using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S111;

/// <summary>S111 · D2 — roles acotados por tenant y por pertenencia a la app (permiso activo de un módulo de la app).</summary>
public sealed class S111RolesPorAppTests
{
    private const int Tenant5 = 5;
    private const int Tenant6 = 6;
    private const int App1 = 1;
    private const int App12 = 12;

    private const int ModuloA = 300;   // app 1
    private const int ModuloB = 301;   // app 12
    private const int ModuloC = 302;   // sin app activa

    private const int PermisoA = 30;
    private const int PermisoB = 31;
    private const int PermisoC = 32;

    private const int R1 = 1;   // t5, permiso de B (app 12)
    private const int R2 = 2;   // t5, permiso de A (app 1)
    private const int R3 = 3;   // global, permiso de B
    private const int R4 = 4;   // t6, permiso de B
    private const int R5 = 5;   // t5, sin permisos
    private const int R6 = 6;   // t5, RolPermiso de B inactivo
    private const int R7 = 7;   // t5, permiso de C (módulo sin app activa)
    private const int R8 = 8;   // t5, inactivo, permiso de B

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync()
    {
        var bd = Guid.NewGuid().ToString();
        await using var c = CrearContext(bd);
        c.Set<Tenant>().AddRange(
            new Tenant { Id = Tenant5, Codigo = "T5", Nombre = "Tenant 5" },
            new Tenant { Id = Tenant6, Codigo = "T6", Nombre = "Tenant 6" });
        c.Set<App>().AddRange(
            new App { Id = App1, Codigo = "PASSPLAT", Nombre = "PassPlat", Activa = true },
            new App { Id = App12, Codigo = "INVENTA", Nombre = "Inventa", Activa = true });
        c.Set<TipoModulo>().Add(new TipoModulo { Id = 1, Codigo = "WEB", Nombre = "Web" });
        c.Set<Modulo>().AddRange(
            new Modulo { Id = ModuloA, Codigo = "MODA", Nombre = "Modulo A", Activo = true, IdTipoModulo = 1, Orden = 1 },
            new Modulo { Id = ModuloB, Codigo = "MODB", Nombre = "Modulo B", Activo = true, IdTipoModulo = 1, Orden = 2 },
            new Modulo { Id = ModuloC, Codigo = "MODC", Nombre = "Modulo C", Activo = true, IdTipoModulo = 1, Orden = 3 });
        c.Set<AppModulo>().AddRange(
            new AppModulo { Id = 1, IdApp = App1, IdModulo = ModuloA },
            new AppModulo { Id = 2, IdApp = App12, IdModulo = ModuloB },
            new AppModulo { Id = 3, IdApp = App12, IdModulo = ModuloC, Activo = false });
        c.Set<Permiso>().AddRange(
            new Permiso { Id = PermisoA, Codigo = "A_VER", Nombre = "Ver A", IdModulo = ModuloA, Activo = true, Orden = 1 },
            new Permiso { Id = PermisoB, Codigo = "B_VER", Nombre = "Ver B", IdModulo = ModuloB, Activo = true, Orden = 2 },
            new Permiso { Id = PermisoC, Codigo = "C_VER", Nombre = "Ver C", IdModulo = ModuloC, Activo = true, Orden = 3 });
        c.Set<Rol>().AddRange(
            new Rol { Id = R1, IdTenant = Tenant5, Codigo = "R1", Nombre = "Rol 1", Activo = true },
            new Rol { Id = R2, IdTenant = Tenant5, Codigo = "R2", Nombre = "Rol 2", Activo = true },
            new Rol { Id = R3, IdTenant = null, Codigo = "R3", Nombre = "Rol 3", Activo = true },
            new Rol { Id = R4, IdTenant = Tenant6, Codigo = "R4", Nombre = "Rol 4", Activo = true },
            new Rol { Id = R5, IdTenant = Tenant5, Codigo = "R5", Nombre = "Rol 5", Activo = true },
            new Rol { Id = R6, IdTenant = Tenant5, Codigo = "R6", Nombre = "Rol 6", Activo = true },
            new Rol { Id = R7, IdTenant = Tenant5, Codigo = "R7", Nombre = "Rol 7", Activo = true },
            new Rol { Id = R8, IdTenant = Tenant5, Codigo = "R8", Nombre = "Rol 8", Activo = false });
        c.Set<RolPermiso>().AddRange(
            new RolPermiso { Id = 1, IdRol = R1, IdPermiso = PermisoB },
            new RolPermiso { Id = 2, IdRol = R2, IdPermiso = PermisoA },
            new RolPermiso { Id = 3, IdRol = R3, IdPermiso = PermisoB },
            new RolPermiso { Id = 4, IdRol = R4, IdPermiso = PermisoB },
            new RolPermiso { Id = 5, IdRol = R6, IdPermiso = PermisoB, Activo = false },
            new RolPermiso { Id = 6, IdRol = R7, IdPermiso = PermisoC },
            new RolPermiso { Id = 7, IdRol = R8, IdPermiso = PermisoB });
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static RolService Servicio(PassPlatDbContext ctx) =>
        new(new RolRepository(ctx, Mock.Of<IUnitOfWorkAsync>()), Mock.Of<IUnitOfWorkAsync>(), Mapper);

    // (1) tenant 5 activos → R1 (propio) y R3 (global); sin alcance e inactivos → también R4 (otro tenant) y R8
    [Theory]
    [InlineData(5, true, new[] { R1, R3 })]
    [InlineData(null, false, new[] { R1, R3, R4, R8 })]
    public async Task Roles_por_apps_acota_por_tenant_y_por_permiso_de_la_app(int? idTenant, bool soloActivos, int[] esperados)
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);

        var resultado = await Servicio(ctx).ObtenerPorAppsAsync(idTenant, [App12], soloActivos, TestContext.Current.CancellationToken);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(esperados, resultado.Value!.Select(r => r.Id).Order().ToArray());
    }

    // (2) lookup: solo activos del tenant y globales con permiso de la app
    [Fact]
    public async Task Lookup_por_apps_devuelve_solo_activos_del_tenant_y_globales()
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);

        var resultado = await Servicio(ctx).ObtenerLookupPorAppsAsync(Tenant5, [App12], TestContext.Current.CancellationToken);

        Assert.True(resultado.IsSuccess);
        Assert.Equal([R1, R3], resultado.Value!.Select(r => r.Id).Order().ToList());
        Assert.All(resultado.Value!, r => Assert.False(string.IsNullOrEmpty(r.Nombre)));
    }
}
