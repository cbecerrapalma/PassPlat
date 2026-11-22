using CBP.Results;
using Moq;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests;

/// <summary>
/// S126 — crítico (aislamiento entre apps): una configuración de proveedor limitada a ciertas apps
/// no aparece en el login de otra, y editar sus apps reemplaza el conjunto sin duplicar enlaces.
/// </summary>
public class S126ProveedoresPorAppTests
{
    private static ConfProvIden Configuracion(params int[] idsApps)
    {
        var conf = new ConfProvIden
        {
            Id = 1,
            IdTenant = 1,
            IdProvIden = 1,
            ClientId = "cliente",
            Activo = true,
            ProvIden = new ProvIden { Id = 1, Codigo = "GOOGLE", Nombre = "Google" }
        };
        conf.AsignarApps(idsApps);
        return conf;
    }

    [Theory]
    [InlineData(new int[0], 12, true)]
    [InlineData(new[] { 12 }, 12, true)]
    [InlineData(new[] { 1, 13 }, 12, false)]
    public async Task ObtenerDisponibles_SoloLasQueSirvenALaApp(int[] idsAppsConfig, int idAppPedida, bool visible)
    {
        var repo = new Mock<IConfProvIdenRepository>();
        repo.Setup(r => r.ObtenerPorTenantAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ConfProvIden>>.Success(new List<ConfProvIden> { Configuracion(idsAppsConfig) }));
        var validador = new Mock<IExternalProviderValidator>();
        validador.Setup(v => v.Validar(It.IsAny<ConfProvIden>()))
            .Returns(new ProviderAvailabilityResult { Disponible = true });
        var configApp = new Mock<IConfigAppRepository>();
        configApp.Setup(c => c.ObtenerPorClaveAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigApp?>.Success(null, allowNull: true));
        var tenants = new Mock<ITenantRepository>();
        var servicio = new ExternalLoginProviderService(repo.Object, validador.Object, configApp.Object, tenants.Object);

        var resultado = await servicio.ObtenerDisponiblesAsync(1, idAppPedida, TestContext.Current.CancellationToken);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(visible, resultado.Value!.Any(p => p.Codigo == "GOOGLE"));
    }

    [Fact]
    public void AsignarApps_ReemplazaElConjunto()
    {
        var conf = Configuracion(1, 12);
        var enlace12 = conf.Apps.Single(a => a.IdApp == 12);

        conf.AsignarApps(new[] { 12, 13, 13, 0 });

        Assert.Equal(new[] { 12, 13 }, conf.Apps.Select(a => a.IdApp).OrderBy(i => i).ToArray());
        Assert.Same(enlace12, conf.Apps.Single(a => a.IdApp == 12));
        Assert.False(conf.SirveParaApp(1));
        Assert.True(conf.SirveParaApp(13));

        conf.AsignarApps(Array.Empty<int>());

        Assert.Empty(conf.Apps);
        Assert.True(conf.SirveParaApp(99));
    }

    // S126 · D9 — crítico (aislamiento entre tenants): respaldo en PLATFORM proveedor por proveedor
    private const int IdPlataforma = 1;
    private const int IdCliente = 2;

    private static ConfProvIden ConfDe(int id, int idTenant, string secreto, params int[] idsApps)
    {
        var conf = new ConfProvIden
        {
            Id = id,
            IdTenant = idTenant,
            IdProvIden = 1,
            ClientId = "cliente",
            ClientSecret = secreto,
            Activo = true,
            ProvIden = new ProvIden { Id = 1, Codigo = "GOOGLE", Nombre = "Google" }
        };
        conf.AsignarApps(idsApps);
        return conf;
    }

    private static ExternalLoginProviderService ServicioConPlataforma(ConfProvIden? delTenant, bool heredar)
    {
        var repo = new Mock<IConfProvIdenRepository>();
        repo.Setup(r => r.ObtenerPorTenantAsync(IdCliente, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ConfProvIden>>.Success(delTenant is null ? new List<ConfProvIden>() : new List<ConfProvIden> { delTenant }));
        repo.Setup(r => r.ObtenerPorTenantAsync(IdPlataforma, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ConfProvIden>>.Success(new List<ConfProvIden> { ConfDe(100, IdPlataforma, "valido") }));
        var validador = new Mock<IExternalProviderValidator>();
        validador.Setup(v => v.Validar(It.IsAny<ConfProvIden>()))
            .Returns((ConfProvIden c) => new ProviderAvailabilityResult { Disponible = c.ClientSecret == "valido" });
        var configApp = new Mock<IConfigAppRepository>();
        configApp.Setup(c => c.ObtenerPorClaveAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigApp?>.Success(new ConfigApp { Clave = "MostrarProveedoresPlataforma", Valor = heredar ? "true" : "false", Activo = true }, allowNull: true));
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.ObtenerPorCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Tenant?>.Success(new Tenant { Id = IdPlataforma }, allowNull: true));
        return new ExternalLoginProviderService(repo.Object, validador.Object, configApp.Object, tenants.Object);
    }

    [Theory]
    [InlineData("valido", true, 7)]      // el tenant tiene la suya válida → la suya, aunque haya herencia
    [InlineData("relleno", true, 100)]   // la suya no es válida → la de PLATFORM
    [InlineData(null, true, 100)]        // no tiene ninguna → la de PLATFORM
    [InlineData(null, false, 0)]         // sin MostrarProveedoresPlataforma → ninguna
    public async Task Resolver_RespaldaEnPlataformaPorProveedor(string? secretoDelTenant, bool heredar, int idConfEsperada)
    {
        var delTenant = secretoDelTenant is null ? null : ConfDe(7, IdCliente, secretoDelTenant);
        var servicio = ServicioConPlataforma(delTenant, heredar);

        var resultado = await servicio.ResolverConfiguracionAsync(IdCliente, 1, 12, TestContext.Current.CancellationToken);
        var lista = await servicio.ObtenerDisponiblesAsync(IdCliente, 12, TestContext.Current.CancellationToken);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(idConfEsperada, resultado.Value?.Id ?? 0);
        Assert.Equal(idConfEsperada == 0 ? 0 : 1, lista.Value!.Count);
        if (idConfEsperada != 0)
            Assert.Equal(idConfEsperada == 100, lista.Value[0].EsDePlataforma);
    }

    [Fact]
    public async Task Resolver_LaDelTenantQueExcluyeLaApp_NoCaeEnLaDePlataforma()
    {
        var servicio = ServicioConPlataforma(ConfDe(7, IdCliente, "valido", 13), heredar: true);

        var resultado = await servicio.ResolverConfiguracionAsync(IdCliente, 1, 12, TestContext.Current.CancellationToken);
        var lista = await servicio.ObtenerDisponiblesAsync(IdCliente, 12, TestContext.Current.CancellationToken);

        Assert.True(resultado.IsFailure);
        Assert.Equal("PROVIDER_NOT_ENABLED_FOR_APP", resultado.Error!.Code);
        Assert.Empty(lista.Value!);
    }
}
