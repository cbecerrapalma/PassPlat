using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S91;

/// <summary>
/// S91 · P51 — grupos y membresías aislados por tenant: con alcance (no-sistema) solo el tenant
/// propio y NotFound fuera de él; sin alcance (is_system) todos los tenants.
/// </summary>
public class S91GruposAislamientoTenantTests
{
    private sealed record Semilla(PassPlatDbContext Ctx, GrupoService Grupos, GrupoUsuarioService Miembros,
        int IdTenantA, int IdTenantB, int IdGrupoA, int IdGrupoB, int IdUsuarioA, int IdUsuarioB, int IdMiembroB);

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
        ctx.Tenants.AddRange(tenantA, tenantB);
        ctx.EstadosUsr.Add(estado);
        ctx.SaveChanges();

        var usuarioA = Usuario.Crear(tenantA.Id, estado.Id, "ana.a", "ana@a.test", "Ana", "A");
        var usuarioB = Usuario.Crear(tenantB.Id, estado.Id, "beto.b", "beto@b.test", "Beto", "B");
        ctx.Usuarios.AddRange(usuarioA, usuarioB);
        ctx.SaveChanges();

        ctx.UsuarioTenants.AddRange(
            UsuarioTenant.Crear(usuarioA.Id, tenantA.Id, estado.Id),
            UsuarioTenant.Crear(usuarioB.Id, tenantB.Id, estado.Id));
        var grupoA = Grupo.Crear(tenantA.Id, "VENTAS", "Ventas");
        var grupoB = Grupo.Crear(tenantB.Id, "BODEGA", "Bodega");
        ctx.Grupos.AddRange(grupoA, grupoB);
        ctx.SaveChanges();

        var miembroB = GrupoUsuario.Crear(grupoB.Id, usuarioB.Id);
        ctx.GruposUsuarios.Add(miembroB);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        var grupoRepo = new GrupoRepository(ctx);
        var grupos = new GrupoService(grupoRepo, new Mock<IUnitOfWorkAsync>().Object, Mapper);
        var miembros = new GrupoUsuarioService(new GrupoUsuarioRepository(ctx), grupoRepo, new UsuarioTenantRepository(ctx), Mapper);

        return new Semilla(ctx, grupos, miembros, tenantA.Id, tenantB.Id, grupoA.Id, grupoB.Id, usuarioA.Id, usuarioB.Id, miembroB.Id);
    }

    private static Grupo Releer(Semilla s, int idGrupo)
    {
        s.Ctx.ChangeTracker.Clear();
        return s.Ctx.Grupos.AsNoTracking().Single(g => g.Id == idGrupo);
    }

    [Fact]
    public async Task ObtenerTodos_con_alcance_devuelve_solo_los_grupos_del_tenant_con_su_nombre()
    {
        var s = Sembrar();

        var r = await s.Grupos.ObtenerTodosAsync(s.IdTenantA, Ct);

        Assert.True(r.IsSuccess);
        var grupo = Assert.Single(r.Value!);
        Assert.Equal(s.IdGrupoA, grupo.Id);
        Assert.Equal("Abarrotes del Sur", grupo.TenantNombre);
    }

    [Fact]
    public async Task ObtenerTodos_sin_alcance_devuelve_los_grupos_de_todos_los_tenants()
    {
        var s = Sembrar();

        var r = await s.Grupos.ObtenerTodosAsync(null, Ct);

        Assert.True(r.IsSuccess);
        Assert.Equal(2, r.Value!.Count);
    }

    [Fact]
    public async Task ObtenerPorTenant_de_otro_tenant_con_alcance_da_NotFound()
    {
        var s = Sembrar();

        var r = await s.Grupos.ObtenerPorTenantAsync(s.IdTenantB, s.IdTenantA, Ct);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
    }

    [Fact]
    public async Task Actualizar_grupo_de_otro_tenant_da_NotFound_y_no_lo_cambia()
    {
        var s = Sembrar();

        var r = await s.Grupos.ActualizarAsync(s.IdGrupoB, new ActualizarGrupoDto { Nombre = "Cambiado", Activo = false }, s.IdTenantA, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
        var grupo = Releer(s, s.IdGrupoB);
        Assert.Equal("Bodega", grupo.Nombre);
        Assert.True(grupo.Activo);
    }

    [Fact]
    public async Task Actualizar_grupo_propio_con_alcance_lo_cambia()
    {
        var s = Sembrar();

        var r = await s.Grupos.ActualizarAsync(s.IdGrupoA, new ActualizarGrupoDto { Nombre = "Ventas Norte", Activo = true }, s.IdTenantA, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsSuccess);
        Assert.Equal("Ventas Norte", Releer(s, s.IdGrupoA).Nombre);
    }

    [Fact]
    public async Task Eliminar_grupo_de_otro_tenant_da_NotFound_y_sigue_activo()
    {
        var s = Sembrar();

        var r = await s.Grupos.EliminarAsync(s.IdGrupoB, s.IdTenantA, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
        Assert.True(Releer(s, s.IdGrupoB).Activo);
    }

    [Fact]
    public async Task MiembrosPorGrupo_de_otro_tenant_con_alcance_da_NotFound()
    {
        var s = Sembrar();

        var r = await s.Miembros.ObtenerPorGrupoAsync(s.IdGrupoB, s.IdTenantA, Ct);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
    }

    [Fact]
    public async Task GruposPorUsuario_con_alcance_omite_los_grupos_de_otro_tenant()
    {
        var s = Sembrar();

        var conAlcance = await s.Miembros.ObtenerPorUsuarioAsync(s.IdUsuarioB, s.IdTenantA, Ct);
        var sinAlcance = await s.Miembros.ObtenerPorUsuarioAsync(s.IdUsuarioB, null, Ct);

        Assert.True(conAlcance.IsSuccess);
        Assert.Empty(conAlcance.Value!);
        Assert.Equal(s.IdGrupoB, Assert.Single(sinAlcance.Value!).IdGrupo);
    }

    [Fact]
    public async Task AgregarMiembro_a_grupo_de_otro_tenant_da_NotFound()
    {
        var s = Sembrar();

        var r = await s.Miembros.AgregarMiembroAsync(new CrearGrupoUsuarioDto { IdGrupo = s.IdGrupoB, IdUsuario = s.IdUsuarioA }, s.IdTenantA, null, Ct);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
    }

    [Fact]
    public async Task AgregarMiembro_usuario_de_otro_tenant_falla_aunque_sea_sistema()
    {
        var s = Sembrar();

        var r = await s.Miembros.AgregarMiembroAsync(new CrearGrupoUsuarioDto { IdGrupo = s.IdGrupoA, IdUsuario = s.IdUsuarioB }, null, null, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsFailure);
        Assert.Equal("SIN_MEMBRESIA_TENANT", r.Error!.Code);
        Assert.False(s.Ctx.GruposUsuarios.Any(gu => gu.IdGrupo == s.IdGrupoA));
    }

    [Fact]
    public async Task AgregarMiembro_usuario_del_tenant_del_grupo_se_agrega()
    {
        var s = Sembrar();

        var r = await s.Miembros.AgregarMiembroAsync(new CrearGrupoUsuarioDto { IdGrupo = s.IdGrupoA, IdUsuario = s.IdUsuarioA }, s.IdTenantA, null, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsSuccess);
        Assert.Single(s.Ctx.GruposUsuarios.Where(gu => gu.IdGrupo == s.IdGrupoA && gu.IdUsuario == s.IdUsuarioA));
    }

    [Fact]
    public async Task RemoverMiembro_de_grupo_de_otro_tenant_da_NotFound()
    {
        var s = Sembrar();

        var r = await s.Miembros.RemoverMiembroAsync(s.IdMiembroB, s.IdTenantA, Ct);
        s.Ctx.SaveChanges();

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorType.NotFound, r.Error!.Type);
        Assert.True(s.Ctx.GruposUsuarios.Any(gu => gu.Id == s.IdMiembroB));
    }
}
