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

namespace PassPlat.Aplicacion.Tests.Tests.S104;

/// <summary>
/// S104 · D2 — el detalle de un permiso (y el listado completo) traen su módulo y su aplicación;
/// un id inexistente es un éxito con valor nulo, no un error.
/// </summary>
public sealed class S104PermisoServiceTests
{
    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<PermisoService> CrearAsync()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        ctx.Set<App>().Add(new App { Id = 1, Codigo = "PASSPLAT", Nombre = "AccessPlat", Activa = true });
        ctx.Set<Modulo>().Add(new Modulo { Id = 100, Codigo = "USR", Nombre = "Usuarios" });
        ctx.Set<AppModulo>().Add(new AppModulo { Id = 1, IdApp = 1, IdModulo = 100 });
        ctx.Set<Permiso>().Add(new Permiso { Id = 5, Codigo = "USR_VER", Nombre = "Ver usuarios", IdModulo = 100, Orden = 1, Activo = true });
        await ctx.SaveChangesAsync(Ct);
        return new PermisoService(new PermisoRepository(ctx), new Mock<IUnitOfWorkAsync>().Object, Mapper);
    }

    [Fact]
    public async Task ObtenerPorId_trae_modulo_y_app()
    {
        var servicio = await CrearAsync();

        var porId = await servicio.ObtenerPorIdAsync(5, Ct);
        var todos = await servicio.ObtenerTodosAsync(Ct);

        Assert.True(porId.IsSuccess);
        Assert.Equal("Usuarios", porId.Value!.ModuloNombre);
        Assert.Equal("USR", porId.Value.ModuloCodigo);
        Assert.Equal(1, porId.Value.IdApp);
        Assert.Equal("PASSPLAT", porId.Value.AppCodigo);
        Assert.Equal("AccessPlat", porId.Value.AppNombre);
        // el listado completo también lleva la app (antes salía nula en todos)
        Assert.Equal("AccessPlat", Assert.Single(todos.Value).AppNombre);
    }

    [Fact]
    public async Task ObtenerPorId_inexistente_devuelve_null_sin_error()
    {
        var servicio = await CrearAsync();

        var resultado = await servicio.ObtenerPorIdAsync(999, Ct);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value);
    }
}
