using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S110;

/// <summary>S110 · D4/D5 — catálogo de módulos y permisos filtrado por lista de apps con Acceso activo.</summary>
public sealed class S110CatalogoPorAppTests
{
    internal const int Tenant = 5;
    internal const int App1 = 1;
    internal const int App12 = 12;
    internal const int ModuloPadreApp1 = 100;
    internal const int ModuloBApp1 = 101;
    internal const int ModuloCApp12 = 200;
    internal const int ModuloHijoApp12 = 201;

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync(Action<PassPlatDbContext>? extra = null)
    {
        var bd = Guid.NewGuid().ToString();
        await using var c = CrearContext(bd);
        c.Set<Tenant>().Add(new Tenant { Id = Tenant, Codigo = "T5", Nombre = "Tenant 5" });
        c.Set<App>().AddRange(
            new App { Id = App1, Codigo = "PASSPLAT", Nombre = "PassPlat", Activa = true },
            new App { Id = App12, Codigo = "INVENTA", Nombre = "Inventa", Activa = true });
        c.Set<TipoModulo>().Add(new TipoModulo { Id = 1, Codigo = "WEB", Nombre = "Web" });
        c.Set<Modulo>().AddRange(
            new Modulo { Id = ModuloPadreApp1, Codigo = "MODA", Nombre = "Modulo A", IdModuloPadre = null, Activo = true, IdTipoModulo = 1, Orden = 1 },
            new Modulo { Id = ModuloBApp1, Codigo = "MODB", Nombre = "Modulo B", IdModuloPadre = null, Activo = true, IdTipoModulo = 1, Orden = 2 },
            new Modulo { Id = ModuloCApp12, Codigo = "MODC", Nombre = "Modulo C", IdModuloPadre = null, Activo = true, IdTipoModulo = 1, Orden = 3 },
            new Modulo { Id = ModuloHijoApp12, Codigo = "MODH", Nombre = "Modulo H", IdModuloPadre = ModuloPadreApp1, Activo = true, IdTipoModulo = 1, Orden = 4 });
        c.Set<AppModulo>().AddRange(
            new AppModulo { Id = 1, IdApp = App1, IdModulo = ModuloPadreApp1 },
            new AppModulo { Id = 2, IdApp = App1, IdModulo = ModuloBApp1 },
            new AppModulo { Id = 3, IdApp = App12, IdModulo = ModuloCApp12 },
            new AppModulo { Id = 4, IdApp = App12, IdModulo = ModuloHijoApp12 });
        c.Set<Permiso>().AddRange(
            new Permiso { Id = 10, Codigo = "A_VER", Nombre = "Ver A", IdModulo = ModuloPadreApp1, Activo = true, Orden = 1 },
            new Permiso { Id = 11, Codigo = "B_VER", Nombre = "Ver B", IdModulo = ModuloBApp1, Activo = true, Orden = 2 },
            new Permiso { Id = 20, Codigo = "C_VER", Nombre = "Ver C", IdModulo = ModuloCApp12, Activo = true, Orden = 3 },
            new Permiso { Id = 21, Codigo = "H_VER", Nombre = "Ver H", IdModulo = ModuloHijoApp12, Activo = true, Orden = 4 });
        extra?.Invoke(c);
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private static readonly IMapper Mapper = new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();
    private static ModuloService ModuloServicio(PassPlatDbContext ctx) => new(new ModuloRepository(ctx), Mock.Of<IUnitOfWorkAsync>(), Mapper);
    private static PermisoService PermisoServicio(PassPlatDbContext ctx) => new(new PermisoRepository(ctx), Mock.Of<IUnitOfWorkAsync>(), Mapper);

    private static IEnumerable<ModuloDto> Aplanar(IEnumerable<ModuloDto> nivel) =>
        nivel.SelectMany(m => Aplanar(m.SubModulos).Prepend(m));

    // (4) módulos A y B de app 1, C e H de app 12: por apps [12] → solo C e H; la asignación inactiva no cuenta
    [Fact]
    public async Task Modulos_por_apps_no_incluye_modulos_de_otra_app()
    {
        const int ModuloInactivoApp12 = 202;
        var bd = await SembrarAsync(c =>
        {
            c.Set<Modulo>().Add(new Modulo { Id = ModuloInactivoApp12, Codigo = "MODI", Nombre = "Modulo I", Activo = true, IdTipoModulo = 1, Orden = 5 });
            c.Set<AppModulo>().Add(new AppModulo { Id = 5, IdApp = App12, IdModulo = ModuloInactivoApp12, Activo = false });
        });
        await using var ctx = CrearContext(bd);
        var ct = TestContext.Current.CancellationToken;

        var arbol = await ModuloServicio(ctx).ObtenerArbolPorAppsAsync([App12], ct);
        Assert.True(arbol.IsSuccess);
        Assert.Equal([ModuloCApp12, ModuloHijoApp12], Aplanar(arbol.Value).Select(m => m.Id).Order().ToList());

        var ambas = await ModuloServicio(ctx).ObtenerArbolPorAppsAsync([App1, App12], ct);
        Assert.True(ambas.IsSuccess);
        Assert.Equal([ModuloPadreApp1, ModuloBApp1, ModuloCApp12, ModuloHijoApp12], Aplanar(ambas.Value).Select(m => m.Id).Order().ToList());
        Assert.Contains(Assert.Single(ambas.Value, m => m.Id == ModuloPadreApp1).SubModulos, m => m.Id == ModuloHijoApp12);

        var ninguna = await ModuloServicio(ctx).ObtenerArbolPorAppsAsync([], ct);
        Assert.True(ninguna.IsSuccess);
        Assert.Empty(ninguna.Value);
    }

    // (6) permisos por apps [12] → solo los de C y H, con AppCode relleno
    [Fact]
    public async Task Permisos_por_apps_filtra_y_rellena_AppCodigo()
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);
        var resultado = await PermisoServicio(ctx).ObtenerPorAppsAsync([App12], true, TestContext.Current.CancellationToken);
        Assert.True(resultado.IsSuccess);
        var permisos = resultado.Value!.OrderBy(p => p.Id).ToList();
        Assert.Equal([20, 21], permisos.Select(p => p.Id).ToList());
        Assert.All(permisos, p =>
        {
            Assert.Equal(App12, p.IdApp);
            Assert.False(string.IsNullOrEmpty(p.AppCodigo));
        });
    }
}
