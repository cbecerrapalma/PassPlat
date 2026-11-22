using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Models;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S95;

/// <summary>
/// S95 · P47 — la auditoría de contraseñas se busca con filtros en la consulta (tenant, usuario,
/// tipo, nivel, fechas y texto) y pagina sobre el total filtrado, no sobre las primeras N filas.
/// </summary>
public class S95AuditoriaPwdFiltrosTests
{
    private sealed record Semilla(AuditoriaPwdService Servicio, int IdTenantA, int IdTenantB, int IdUsuarioA, int IdTipoLogin, int IdTipoCambio);

    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Semilla Sembrar()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tenantA = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var tenantB = new Tenant { Codigo = "VESTUARIO", Nombre = "Vestuario SA de CV" };
        var estado = new EstadoUsr { Codigo = "ACTIVO", Nombre = "Activo" };
        var login = TipoAuditoria.Crear("Login");
        var cambio = TipoAuditoria.Crear("Cambio de clave");
        ctx.Tenants.AddRange(tenantA, tenantB);
        ctx.EstadosUsr.Add(estado);
        ctx.TiposAuditoria.AddRange(login, cambio);
        ctx.SaveChanges();

        var ana = Usuario.Crear(tenantA.Id, estado.Id, "ana.a", "ana@a.test", "Ana", "A");
        var beto = Usuario.Crear(tenantB.Id, estado.Id, "beto.b", "beto@b.test", "Beto", "B");
        ctx.Usuarios.AddRange(ana, beto);
        ctx.SaveChanges();

        // Tenant A: 12 filas de Ana, una por día desde el 2026-10-01; la del día 5 es un cambio de riesgo 3.
        for (var dia = 1; dia <= 12; dia++)
        {
            var fila = AuditoriaPwd.Crear(ana.Id, dia == 5 ? cambio.Id : login.Id);
            fila.IdTenant = tenantA.Id;
            fila.FecAccion = new DateTime(2026, 10, dia, 10, 0, 0);
            fila.NivelRiesgo = dia == 5 ? 3 : 1;
            fila.Detalles = dia == 5 ? "Cambio tras bloqueo" : "Ingreso correcto";
            ctx.AuditoriaPwd.Add(fila);
        }
        // Tenant B: 3 filas de Beto.
        for (var dia = 1; dia <= 3; dia++)
        {
            var fila = AuditoriaPwd.Crear(beto.Id, login.Id);
            fila.IdTenant = tenantB.Id;
            fila.FecAccion = new DateTime(2026, 10, dia, 11, 0, 0);
            fila.Detalles = "Ingreso correcto";
            ctx.AuditoriaPwd.Add(fila);
        }
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        return new Semilla(new AuditoriaPwdService(new AuditoriaPwdRepository(ctx), Mapper),
            tenantA.Id, tenantB.Id, ana.Id, login.Id, cambio.Id);
    }

    [Fact]
    public async Task Buscar_con_tenant_devuelve_solo_sus_filas_y_pagina_sobre_el_total()
    {
        var s = Sembrar();

        var pagina2 = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantA), 2, 5, Ct);
        var pagina3 = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantA), 3, 5, Ct);

        Assert.True(pagina2.IsSuccess);
        Assert.Equal(12, pagina2.Value.TotalCount);
        Assert.Equal(5, pagina2.Value.Items.Count);
        Assert.All(pagina2.Value.Items, a => Assert.Equal(s.IdTenantA, a.IdTenant));
        Assert.Equal(2, pagina3.Value.Items.Count);
    }

    [Fact]
    public async Task Buscar_sin_tenant_devuelve_las_filas_de_todos_los_tenants()
    {
        var s = Sembrar();

        var r = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(), 1, 50, Ct);

        Assert.True(r.IsSuccess);
        Assert.Equal(15, r.Value.TotalCount);
        Assert.Contains(r.Value.Items, a => a.IdTenant == s.IdTenantB);
    }

    [Fact]
    public async Task Buscar_por_tipo_nivel_y_texto_filtra_y_trae_los_nombres()
    {
        var s = Sembrar();

        var porTipo = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantA, IdTipoAccion: s.IdTipoCambio), 1, 20, Ct);
        var porNivel = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(NivelMinimo: 3), 1, 20, Ct);
        var porMensaje = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(Texto: "bloqueo"), 1, 20, Ct);
        var porUsuario = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(Texto: "beto"), 1, 20, Ct);

        var cambio = Assert.Single(porTipo.Value.Items);
        Assert.Equal("Cambio de clave", cambio.TipoAccionNombre);
        Assert.Equal("ana.a", cambio.UsuarioNombre);
        Assert.Single(porNivel.Value.Items);
        Assert.Single(porMensaje.Value.Items);
        Assert.Equal(3, porUsuario.Value.TotalCount);
    }

    [Fact]
    public async Task Buscar_por_usuario_devuelve_solo_sus_filas_dentro_del_tenant()
    {
        var s = Sembrar();

        var enSuTenant = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantA, IdUsuario: s.IdUsuarioA), 1, 20, Ct);
        var enOtroTenant = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantB, IdUsuario: s.IdUsuarioA), 1, 20, Ct);

        Assert.Equal(12, enSuTenant.Value.TotalCount);
        Assert.Empty(enOtroTenant.Value.Items);
    }

    [Fact]
    public async Task Buscar_por_rango_de_fechas_incluye_el_dia_hasta_completo()
    {
        var s = Sembrar();

        var r = await s.Servicio.BuscarAsync(
            new FiltroAuditoriaPwd(IdTenant: s.IdTenantA, Desde: new DateTime(2026, 10, 3), Hasta: new DateTime(2026, 10, 5)), 1, 20, Ct);

        Assert.Equal(3, r.Value.TotalCount);
        Assert.All(r.Value.Items, a => Assert.InRange(a.FecAccion!.Value.Day, 3, 5));
    }

    [Fact]
    public async Task Buscar_ordena_de_la_mas_reciente_y_corrige_pagina_y_tamano_invalidos()
    {
        var s = Sembrar();

        var r = await s.Servicio.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: s.IdTenantA), 0, 0, Ct);

        Assert.Equal(1, r.Value.PageNumber);
        Assert.Equal(20, r.Value.PageSize);
        Assert.Equal(12, r.Value.Items.Count);
        Assert.Equal(12, r.Value.Items[0].FecAccion!.Value.Day);
    }
}
