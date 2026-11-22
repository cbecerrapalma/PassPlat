using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results.Errors;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Entities.Catalogos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S110;

/// <summary>S110 · D2 — apps permitidas por usuario y tenant (acotamiento de api/modulos y api/permisos por idApp).</summary>
public sealed class S110AppsPermitidasTests
{
    internal const int Tenant = 5;
    internal const int OtroTenant = 6;
    internal const int Usuario = 71;
    internal const int Sistema = 72;
    internal const int AppPassplat = 1;
    internal const int AppInventa = 12;
    internal const int AppSinAcceso = 2;

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync(Action<PassPlatDbContext>? extra = null)
    {
        var bd = Guid.NewGuid().ToString();
        await using var c = CrearContext(bd);
        c.Set<Tenant>().AddRange(
            new Tenant { Id = Tenant, Codigo = "T5", Nombre = "Tenant 5" },
            new Tenant { Id = OtroTenant, Codigo = "T6", Nombre = "Tenant 6" });
        c.Set<App>().AddRange(
            new App { Id = AppPassplat, Codigo = "PASSPLAT", Nombre = "PassPlat", Activa = true },
            new App { Id = AppInventa, Codigo = "INVENTA", Nombre = "Inventa", Activa = true },
            new App { Id = AppSinAcceso, Codigo = "SINACC", Nombre = "Sin acceso", Activa = true });
        c.Set<EstadoUsr>().Add(new EstadoUsr { Id = 1, Codigo = "ACT", Nombre = "Activo" });
        c.Set<Rol>().Add(new Rol { Id = 10, IdTenant = Tenant, Codigo = "ADM5", Nombre = "Admin T5" });
        c.Set<Usuario>().AddRange(
            new Usuario { Id = Usuario, IdTenant = Tenant, NomUsuario = "u71", EsSistema = false },
            new Usuario { Id = Sistema, IdTenant = Tenant, NomUsuario = "sys", EsSistema = true });
        c.Set<UsuarioTenant>().AddRange(
            new UsuarioTenant { Id = 1, IdUsuario = Usuario, IdTenant = Tenant, IdEstado = 1, Activo = true },
            new UsuarioTenant { Id = 2, IdUsuario = Sistema, IdTenant = Tenant, IdEstado = 1, Activo = true });
        extra?.Invoke(c);
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private static UsuarioPermisoService Servicio(PassPlatDbContext ctx) => new(
        new UsuarioPermisoRepository(ctx),
        new AccesoRepository(ctx),
        new UsuarioTenantRepository(ctx),
        new PermisoRepository(ctx),
        new AuthRepository(ctx, Mock.Of<IUnitOfWorkAsync>()),
        Mock.Of<IMapper>());

    private static Acceso AccesoRow(int id, int idUsuario, int idTenant, int idApp, int idRol = 10) =>
        new() { Id = id, IdUsuario = idUsuario, IdTenant = idTenant, IdApp = idApp, IdRol = idRol };

    // (1) Sistema: sin idApp → null (todas); con idApp → [idApp] sin validar acceso
    [Fact]
    public async Task Sistema_sin_idApp_devuelve_null_y_con_idApp_devuelve_esa_app()
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);
        var servicio = Servicio(ctx);
        var ct = TestContext.Current.CancellationToken;

        var sinApp = await servicio.ObtenerAppsPermitidasAsync(Sistema, null, null, ct);
        Assert.True(sinApp.IsSuccess);
        Assert.Null(sinApp.Value);

        var conApp = await servicio.ObtenerAppsPermitidasAsync(Sistema, null, AppInventa, ct);
        Assert.True(conApp.IsSuccess);
        Assert.Equal([AppInventa], conApp.Value);
    }

    // (2) No-sistema sin idApp → solo apps con Acceso activo en su tenant; nunca null (ni "todas")
    [Fact]
    public async Task NoSistema_sin_idApp_devuelve_solo_las_apps_con_acceso_en_su_tenant()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().AddRange(
            AccesoRow(1, Usuario, Tenant, AppPassplat),
            AccesoRow(2, Usuario, Tenant, AppInventa),
            AccesoRow(3, Usuario, OtroTenant, AppSinAcceso)));
        await using var ctx = CrearContext(bd);
        var servicio = Servicio(ctx);
        var ct = TestContext.Current.CancellationToken;

        var apps = await servicio.ObtenerAppsPermitidasAsync(Usuario, Tenant, null, ct);
        Assert.True(apps.IsSuccess);
        Assert.Equal([AppPassplat, AppInventa], apps.Value?.OrderBy(a => a).ToList() ?? []);

        // Cero apps con Acceso: lista vacía (veredicto vuelta 2), nunca fuga de "todas"
        var vacio = await servicio.ObtenerAppsPermitidasAsync(999, Tenant, null, ct);
        Assert.True(vacio.IsSuccess);
        Assert.Empty(vacio.Value ?? []);
    }

    // (3) No-sistema con idApp sin acceso en su tenant → SIN_ACCESO_APP, 403
    [Fact]
    public async Task NoSistema_con_idApp_sin_acceso_devuelve_SIN_ACCESO_APP_403()
    {
        var bd = await SembrarAsync(c => c.Set<Acceso>().Add(AccesoRow(1, Usuario, Tenant, AppPassplat)));
        await using var ctx = CrearContext(bd);
        var servicio = Servicio(ctx);
        var ct = TestContext.Current.CancellationToken;

        var sinAcceso = await servicio.ObtenerAppsPermitidasAsync(Usuario, Tenant, AppSinAcceso, ct);
        Assert.False(sinAcceso.IsSuccess);
        Assert.Equal("SIN_ACCESO_APP", sinAcceso.Error!.Code);
        Assert.Equal(403, sinAcceso.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Forbidden, sinAcceso.Error.Type);

        var conAcceso = await servicio.ObtenerAppsPermitidasAsync(Usuario, Tenant, AppPassplat, ct);
        Assert.True(conAcceso.IsSuccess);
        Assert.Equal([AppPassplat], conAcceso.Value);
    }
}
