using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S103;

/// <summary>
/// S103 · D1-D4 — editor unificado de permisos de un usuario por (tenant, app): contextos con Acceso activo,
/// permisos por rol (con herencia), directos concedidos/denegados y una sola aplicación todo-o-nada.
/// </summary>
public sealed class S103PermisosUsuarioEditorTests
{
    internal const int Tenant = 2;
    internal const int App = 1;
    internal const int Usuario = 7;
    internal const int Sistema = 8;

    private readonly Mock<IUnitOfWorkAsync> _uow = new();

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    /// <summary>
    /// App 1 y 2 activas, 3 inactiva; tenants 2 y 4 con membresía del usuario 7 y 5 sin ella; permisos 1-5 en la app 1
    /// y 20 en la app 2; el rol 10 da los permisos 1 y 3 y el rol 11 el 2.
    /// </summary>
    internal static async Task<string> SembrarAsync(Action<PassPlatDbContext>? extra = null)
    {
        var bd = Guid.NewGuid().ToString();
        await using var c = CrearContext(bd);
        c.Set<Tenant>().AddRange(
            new Tenant { Id = Tenant, Codigo = "VEST", Nombre = "Vestuario" },
            new Tenant { Id = 4, Codigo = "ZETA", Nombre = "Zeta" },
            new Tenant { Id = 5, Codigo = "CINCO", Nombre = "Cinco" });
        c.Set<App>().AddRange(
            new App { Id = App, Codigo = "PASSPLAT", Nombre = "AccessPlat", Activa = true },
            new App { Id = 2, Codigo = "INVENTA", Nombre = "Inventa", Activa = true },
            new App { Id = 3, Codigo = "VIEJA", Nombre = "App vieja", Activa = false });
        c.Set<Usuario>().AddRange(
            new Usuario { Id = Usuario, IdTenant = Tenant, NomUsuario = "u7" },
            new Usuario { Id = Sistema, IdTenant = Tenant, NomUsuario = "sys", EsSistema = true });
        c.Set<EstadoUsr>().Add(new EstadoUsr { Id = 1, Codigo = "ACT", Nombre = "Activo" });
        c.Set<UsuarioTenant>().AddRange(
            new UsuarioTenant { Id = 1, IdUsuario = Usuario, IdTenant = Tenant, IdEstado = 1 },
            new UsuarioTenant { Id = 2, IdUsuario = Usuario, IdTenant = 4, IdEstado = 1 },
            new UsuarioTenant { Id = 3, IdUsuario = Sistema, IdTenant = Tenant, IdEstado = 1 });
        c.Set<Rol>().AddRange(
            new Rol { Id = 10, IdTenant = Tenant, Codigo = "ADM", Nombre = "Admin Vestuario" },
            new Rol { Id = 11, IdTenant = Tenant, Codigo = "BASE", Nombre = "Base" });
        c.Set<Modulo>().AddRange(
            new Modulo { Id = 100, Codigo = "USR", Nombre = "Usuarios" },
            new Modulo { Id = 200, Codigo = "INV", Nombre = "Inventario" });
        c.Set<AppModulo>().AddRange(
            new AppModulo { Id = 1, IdApp = App, IdModulo = 100 },
            new AppModulo { Id = 2, IdApp = 2, IdModulo = 200 });
        c.Set<Permiso>().AddRange(
            new Permiso { Id = 1, Codigo = "USR_VER", Nombre = "Ver", IdModulo = 100, Orden = 1 },
            new Permiso { Id = 2, Codigo = "USR_EDITAR", Nombre = "Editar", IdModulo = 100, Orden = 2 },
            new Permiso { Id = 3, Codigo = "USR_BORRAR", Nombre = "Borrar", IdModulo = 100, Orden = 3 },
            new Permiso { Id = 4, Codigo = "USR_EXPORTAR", Nombre = "Exportar", IdModulo = 100, Orden = 4 },
            new Permiso { Id = 5, Codigo = "USR_IMPORTAR", Nombre = "Importar", IdModulo = 100, Orden = 5 },
            new Permiso { Id = 20, Codigo = "INV_VER", Nombre = "Ver inventario", IdModulo = 200, Orden = 1 });
        c.Set<RolPermiso>().AddRange(
            new RolPermiso { Id = 1, IdRol = 10, IdPermiso = 1 },
            new RolPermiso { Id = 2, IdRol = 10, IdPermiso = 3 },
            new RolPermiso { Id = 3, IdRol = 11, IdPermiso = 2 });
        extra?.Invoke(c);
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    internal static Acceso Acceso(int id, int idUsuario, int idTenant, int idApp, int idRol = 10) =>
        new() { Id = id, IdUsuario = idUsuario, IdTenant = idTenant, IdApp = idApp, IdRol = idRol };

    private static UsuarioPermiso Directo(int id, int idPermiso, byte tipo, DateTime? inicio = null, DateTime? fin = null) =>
        new() { Id = id, IdUsuario = Usuario, IdPermiso = idPermiso, IdTenant = Tenant, IdApp = App, IdTipoAsig = tipo, FecInicio = inicio, FecFin = fin };

    internal static UsuarioPermisoService CrearServicio(PassPlatDbContext ctx, IUnitOfWorkAsync uow) => new(
        new UsuarioPermisoRepository(ctx), new AccesoRepository(ctx), new UsuarioTenantRepository(ctx), new PermisoRepository(ctx),
        new AuthRepository(ctx, uow), Mock.Of<IMapper>());

    private UsuarioPermisoService Servicio(PassPlatDbContext ctx) => CrearServicio(ctx, _uow.Object);

    private static AplicarPermisosUsuarioDto Aplicar(int idUsuario, params (int Permiso, bool Tiene)[] cambios) => new()
    {
        IdUsuario = idUsuario,
        IdTenant = Tenant,
        IdApp = App,
        Cambios = [.. cambios.Select(c => new CambioPermisoDto { IdPermiso = c.Permiso, Tiene = c.Tiene })]
    };

    private static void SinCambiosPendientes(PassPlatDbContext ctx) =>
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State is EntityState.Added or EntityState.Modified);

    [Fact]
    public async Task Editor_marca_por_rol_agregados_quitados_y_calcula_Tiene()
    {
        var ayer = DateTime.Now.AddDays(-1);
        var bd = await SembrarAsync(c =>
        {
            c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App));
            c.Set<UsuarioPermiso>().AddRange(
                Directo(1, 2, 1),                       // agregado vigente
                Directo(2, 3, 2),                       // quitado a un permiso del rol
                Directo(3, 4, 1, ayer.AddDays(-5), ayer)); // agregado vencido
        });
        await using var ctx = CrearContext(bd);

        var editor = await Servicio(ctx).ObtenerEditorAsync(Usuario, Tenant, App, TestContext.Current.CancellationToken);

        Assert.True(editor.IsSuccess);
        var p = editor.Value.Permisos.ToDictionary(x => x.IdPermiso);
        Assert.Equal([1, 2, 3, 4, 5], p.Keys.Order());             // el permiso 20 es de otra app
        Assert.True(p[1].PorRol && p[1].Tiene);
        Assert.Equal("Admin Vestuario", p[1].RolOrigen);
        Assert.False(p[2].PorRol);
        Assert.True(p[2].Tiene);
        Assert.Equal((byte)1, p[2].IdTipoAsigDirecto);
        Assert.True(p[3].PorRol);
        Assert.False(p[3].Tiene);
        Assert.Equal((byte)2, p[3].IdTipoAsigDirecto);
        Assert.False(p[4].Tiene);
        Assert.False(p[4].DirectoVigente);
        Assert.Null(p[5].IdTipoAsigDirecto);
        Assert.False(p[5].Tiene);
        Assert.Equal("Vestuario", editor.Value.TenantNombre);
        Assert.Equal("AccessPlat", editor.Value.AppNombre);
        Assert.Equal(["Admin Vestuario"], editor.Value.Roles.Select(r => r.Nombre));
    }

    [Fact]
    public async Task Editor_incluye_permisos_de_roles_heredados_sin_ciclos()
    {
        var bd = await SembrarAsync(c =>
        {
            c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App));
            // el rol 10 hereda del 11 y el 11 del 10: el ciclo no debe colgar la consulta
            c.Set<RolesHerencia>().AddRange(
                new RolesHerencia { Id = 1, IdRolHijo = 10, IdRolPadre = 11, IdTenant = Tenant },
                new RolesHerencia { Id = 2, IdRolHijo = 11, IdRolPadre = 10, IdTenant = Tenant });
        });
        await using var ctx = CrearContext(bd);

        var editor = await Servicio(ctx).ObtenerEditorAsync(Usuario, Tenant, App, TestContext.Current.CancellationToken);

        Assert.True(editor.IsSuccess);
        var heredado = editor.Value.Permisos.Single(x => x.IdPermiso == 2);
        Assert.True(heredado.PorRol);
        Assert.True(heredado.Tiene);
        Assert.Equal("Base (heredado)", heredado.RolOrigen);
        Assert.Equal("Admin Vestuario", editor.Value.Permisos.Single(x => x.IdPermiso == 1).RolOrigen);
    }

    [Fact]
    public async Task Editor_sin_contexto_valido_devuelve_SIN_ACCESO_APP()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().AddRange(
            Acceso(1, Usuario, Tenant, App),
            Acceso(2, Usuario, Tenant, 3)));          // app inactiva
        await using var ctx = CrearContext(bd);
        var servicio = Servicio(ctx);
        var ct = TestContext.Current.CancellationToken;

        var appInactiva = await servicio.ObtenerEditorAsync(Usuario, Tenant, 3, ct);
        var otroTenant = await servicio.ObtenerEditorAsync(Usuario, 4, App, ct);
        var otraApp = await servicio.ObtenerEditorAsync(Usuario, Tenant, 2, ct);

        foreach (var r in new[] { appInactiva, otroTenant, otraApp })
        {
            Assert.True(r.IsFailure);
            Assert.Equal("SIN_ACCESO_APP", r.Error!.Code);
        }
    }

    [Fact]
    public async Task Contextos_solo_apps_activas_con_acceso_membresia_y_alcance()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().AddRange(
            Acceso(1, Usuario, Tenant, App),
            Acceso(2, Usuario, Tenant, 2),
            Acceso(3, Usuario, Tenant, 3),            // app inactiva
            Acceso(4, Usuario, 4, App),
            Acceso(5, Usuario, 5, App),               // sin membresía en el tenant 5
            new Acceso { Id = 6, IdUsuario = Usuario, IdTenant = Tenant, IdApp = 2, IdRol = 11, Activo = false }));
        await using var ctx = CrearContext(bd);
        var servicio = Servicio(ctx);
        var ct = TestContext.Current.CancellationToken;

        var todos = await servicio.ObtenerContextosAsync(Usuario, null, ct);
        var propio = await servicio.ObtenerContextosAsync(Usuario, Tenant, ct);
        var ajeno = await servicio.ObtenerContextosAsync(Usuario, 5, ct);

        Assert.True(todos.IsSuccess);
        Assert.Equal([(Tenant, App), (Tenant, 2), (4, App)], todos.Value.Select(x => (x.IdTenant, x.IdApp)));
        Assert.Equal(["Admin Vestuario"], todos.Value[0].Roles.Select(r => r.Nombre));
        Assert.Equal([(Tenant, App), (Tenant, 2)], propio.Value.Select(x => (x.IdTenant, x.IdApp)));
        Assert.Empty(ajeno.Value);
    }

    [Fact]
    public async Task Aplicar_dar_sin_rol_crea_Conceder_con_la_app()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App)));
        await using var ctx = CrearContext(bd);
        var inicio = DateTime.Now.AddDays(1);
        var fin = inicio.AddDays(30);
        var dto = Aplicar(Usuario, (2, true));
        dto.Motivo = "Cobertura";
        dto.FecInicio = inicio;
        dto.FecFin = fin;

        var result = await Servicio(ctx).AplicarPermisosAsync(dto, 99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Concedidos);
        var fila = Assert.Single(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State == EntityState.Added).Entity;
        Assert.Equal((byte)1, fila.IdTipoAsig);
        Assert.Equal(App, fila.IdApp);
        Assert.Equal((Usuario, 2, Tenant), (fila.IdUsuario, fila.IdPermiso, fila.IdTenant));
        Assert.Equal("Cobertura", fila.Motivo);
        Assert.Equal((inicio, fin), (fila.FecInicio, fila.FecFin));
        Assert.Equal(99, fila.IdUsrCrea);
    }

    [Fact]
    public async Task Aplicar_quitar_permiso_de_rol_crea_Denegar()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App)));
        await using var ctx = CrearContext(bd);

        var result = await Servicio(ctx).AplicarPermisosAsync(Aplicar(Usuario, (1, false), (5, false)), 99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Denegados);
        Assert.Equal(1, result.Value.SinCambio);            // el 5 no lo tiene ni por rol ni directo
        var fila = Assert.Single(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State == EntityState.Added).Entity;
        Assert.Equal((byte)2, fila.IdTipoAsig);
        Assert.Equal((1, App), (fila.IdPermiso, fila.IdApp));
    }

    [Fact]
    public async Task Aplicar_revierte_excepciones_en_su_misma_fila()
    {
        var bd = await SembrarAsync(c =>
        {
            c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App));
            c.Set<UsuarioPermiso>().AddRange(
                Directo(1, 1, 2),    // quitado a un permiso del rol → se pide tenerlo: desactivar
                Directo(2, 2, 1),    // agregado sin rol → se pide no tenerlo: desactivar
                Directo(3, 3, 1),    // agregado a uno del rol → se pide no tenerlo: la misma fila pasa a Denegar
                Directo(4, 5, 2));   // quitado sin rol → se pide tenerlo: la misma fila pasa a Conceder
        });
        await using var ctx = CrearContext(bd);

        var result = await Servicio(ctx).AplicarPermisosAsync(
            Aplicar(Usuario, (1, true), (2, false), (3, false), (5, true)), 99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, 1, 2, 0), (result.Value.Concedidos, result.Value.Denegados, result.Value.Revocados, result.Value.SinCambio));
        var filas = ctx.ChangeTracker.Entries<UsuarioPermiso>().ToList();
        Assert.DoesNotContain(filas, e => e.State == EntityState.Added);
        UsuarioPermiso F(int id) => filas.Single(e => e.Entity.Id == id).Entity;
        Assert.False(F(1).Activo);
        Assert.False(F(2).Activo);
        Assert.True(F(3).Activo);
        Assert.Equal((byte)2, F(3).IdTipoAsig);
        Assert.True(F(4).Activo);
        Assert.Equal((byte)1, F(4).IdTipoAsig);
        Assert.All(filas, e => Assert.Equal(99, e.Entity.IdUsrMod));
    }

    [Fact]
    public async Task Aplicar_con_permiso_de_otra_app_no_cambia_nada()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().Add(Acceso(1, Usuario, Tenant, App)));
        await using var ctx = CrearContext(bd);

        var result = await Servicio(ctx).AplicarPermisosAsync(Aplicar(Usuario, (1, false), (20, true)), 99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("PERMISO_FUERA_DE_APP", result.Error!.Code);
        SinCambiosPendientes(ctx);
    }

    [Fact]
    public async Task Aplicar_a_usuario_de_sistema_falla()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().Add(Acceso(1, Sistema, Tenant, App)));
        await using var ctx = CrearContext(bd);

        var result = await Servicio(ctx).AplicarPermisosAsync(Aplicar(Sistema, (2, true)), 99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("USUARIO_SISTEMA", result.Error!.Code);
        SinCambiosPendientes(ctx);
    }
}
