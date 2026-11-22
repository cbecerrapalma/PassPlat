using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S83;

/// <summary>
/// S83 · D3 — menú de la app (<c>GET api/modulos/menu</c>): módulos activos de la app del token,
/// visibles según los permisos <c>_VER</c> del usuario; el tipo de módulo no decide la visibilidad.
/// </summary>
public class S83MenuModulosTests
{
    private const int AppPassPlat = 1;
    private const int OtraApp = 12;

    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static Modulo Modulo(int id, int? padre, string codigo, string? ruta, int tipo = 2, short orden = 0, bool activo = true) => new()
    {
        Id = id,
        IdModuloPadre = padre,
        IdTipoModulo = tipo,
        Codigo = codigo,
        Nombre = codigo,
        Ruta = ruta,
        Orden = orden,
        Activo = activo
    };

    private static int _idPermiso;

    private static Permiso Permiso(int idModulo, string codigo, bool activo = true) =>
        new() { Id = Interlocked.Increment(ref _idPermiso), IdModulo = idModulo, Codigo = codigo, Nombre = codigo, Activo = activo };

    private static AppModulo Asociacion(int idApp, int idModulo, bool activo = true) =>
        new() { Id = idApp * 10_000 + idModulo, IdApp = idApp, IdModulo = idModulo, Activo = activo };

    /// <summary>Árbol de PASSPLAT: IAM(100) → Usuarios(101), Panel(109, sin permisos); Plataforma(120, tipo 1) → Tenants(121).</summary>
    private static async Task<PassPlatDbContext> SembrarAsync(Action<PassPlatDbContext>? extra = null)
    {
        var ctx = CrearContext();
        ctx.Set<Modulo>().AddRange(
            Modulo(100, null, "IAM", null, orden: 10),
            Modulo(101, 100, "IAM.USUARIOS", "/usuarios", orden: 11),
            Modulo(109, 101, "IAM.PANEL", "/admin/panel", orden: 11),
            Modulo(120, null, "PLATAFORMA", null, tipo: 1, orden: 30),
            Modulo(121, 120, "PLATAFORMA.TENANTS", "/tenants", tipo: 1, orden: 31));
        ctx.Set<Permiso>().AddRange(
            Permiso(101, "USUARIOS_VER"),
            Permiso(101, "USUARIOS_CREAR"),
            Permiso(121, "TENANTS_VER"));
        foreach (var id in new[] { 100, 101, 109, 120, 121 })
            ctx.Set<AppModulo>().Add(Asociacion(AppPassPlat, id));
        extra?.Invoke(ctx);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return ctx;
    }

    private static ModuloService Servicio(PassPlatDbContext ctx) =>
        new(new ModuloRepository(ctx), new Mock<IUnitOfWorkAsync>().Object, new Mock<IMapper>().Object);

    private static async Task<IReadOnlyList<ModuloMenuDto>> MenuAsync(PassPlatDbContext ctx, bool esSistema, params string[] permisos)
    {
        var result = await Servicio(ctx).ObtenerMenuAsync(AppPassPlat, permisos.ToHashSet(StringComparer.Ordinal), esSistema, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static List<string> Codigos(IEnumerable<ModuloMenuDto> nodos) =>
        nodos.SelectMany(n => new[] { n.Codigo }.Concat(Codigos(n.Hijos))).ToList();

    [Fact]
    public async Task Usuario_sin_permiso_VER_del_modulo_no_lo_ve()
    {
        await using var ctx = await SembrarAsync();

        var codigos = Codigos(await MenuAsync(ctx, false, "TENANTS_VER"));

        Assert.DoesNotContain("IAM.USUARIOS", codigos);
        Assert.DoesNotContain("IAM", codigos);
        Assert.Contains("PLATAFORMA.TENANTS", codigos);
    }

    [Fact]
    public async Task Permiso_que_no_es_VER_no_da_visibilidad()
    {
        await using var ctx = await SembrarAsync();

        var codigos = Codigos(await MenuAsync(ctx, false, "USUARIOS_CREAR"));

        Assert.DoesNotContain("IAM.USUARIOS", codigos);
        Assert.Empty(codigos);
    }

    [Fact]
    public async Task Modulo_sin_permisos_VER_hereda_del_padre()
    {
        await using var ctx = await SembrarAsync();

        var conPermiso = Codigos(await MenuAsync(ctx, false, "USUARIOS_VER"));
        var sinPermiso = Codigos(await MenuAsync(ctx, false, "TENANTS_VER"));

        Assert.Contains("IAM.PANEL", conPermiso);
        Assert.DoesNotContain("IAM.PANEL", sinPermiso);
    }

    [Fact]
    public async Task Modulo_de_tipo_sistema_es_visible_con_su_permiso()
    {
        await using var ctx = await SembrarAsync();

        var menu = await MenuAsync(ctx, false, "TENANTS_VER");

        var plataforma = Assert.Single(menu);
        Assert.Equal("PLATAFORMA", plataforma.Codigo);
        Assert.Equal("/tenants", Assert.Single(plataforma.Hijos).Ruta);
    }

    [Fact]
    public async Task Modulo_sin_ruta_ni_hijos_visibles_se_omite()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(Modulo(700, null, "DASHBOARDS", null, orden: 70), Modulo(136, 100, "EMAIL.HISTORIAL", null));
            c.Set<Permiso>().Add(Permiso(136, "EMAIL_LOG_VER"));
            c.Set<AppModulo>().AddRange(Asociacion(AppPassPlat, 700), Asociacion(AppPassPlat, 136));
        });

        var codigos = Codigos(await MenuAsync(ctx, true));

        Assert.DoesNotContain("DASHBOARDS", codigos);
        Assert.DoesNotContain("EMAIL.HISTORIAL", codigos);
        Assert.Contains("IAM", codigos);
    }

    [Fact]
    public async Task Usuario_de_sistema_ve_todo()
    {
        await using var ctx = await SembrarAsync();

        var menu = await MenuAsync(ctx, true);

        Assert.Equal(["IAM", "IAM.USUARIOS", "IAM.PANEL", "PLATAFORMA", "PLATAFORMA.TENANTS"], Codigos(menu));
        Assert.Equal(["IAM", "PLATAFORMA"], menu.Select(m => m.Codigo));
    }

    [Fact]
    public async Task Repositorio_no_devuelve_modulos_de_otra_app()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(Modulo(300, null, "INVENTA", null), Modulo(301, 300, "INVENTA.PRODUCTOS", "/productos"));
            c.Set<AppModulo>().AddRange(Asociacion(OtraApp, 300), Asociacion(OtraApp, 301));
        });

        var result = await new ModuloRepository(ctx).ObtenerMenuPorAppAsync(AppPassPlat, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value, m => m.Codigo.StartsWith("INVENTA", StringComparison.Ordinal));
        Assert.DoesNotContain("INVENTA.PRODUCTOS", Codigos(await MenuAsync(ctx, true)));
    }

    [Fact]
    public async Task Repositorio_omite_modulo_o_asociacion_inactivos()
    {
        await using var ctx = await SembrarAsync(c =>
        {
            c.Set<Modulo>().AddRange(Modulo(140, null, "AUDITORIA", null, activo: false), Modulo(141, null, "AUDITORIA.EVENTOS", "/auditoria"));
            c.Set<AppModulo>().AddRange(Asociacion(AppPassPlat, 140), Asociacion(AppPassPlat, 141, activo: false));
        });

        var result = await new ModuloRepository(ctx).ObtenerMenuPorAppAsync(AppPassPlat, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value, m => m.Id is 140 or 141);
        Assert.Equal(5, result.Value.Count);
    }
}
