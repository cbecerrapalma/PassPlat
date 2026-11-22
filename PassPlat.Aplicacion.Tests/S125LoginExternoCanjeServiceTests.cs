using CBP.Caching.DependencyInjection;
using CBP.Caching.Interfaces;
using CBP.Caching.Memory;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests;

/// <summary>
/// S125 — crítico (seguridad): el login externo solo vuelve a la base de la App (sin redirección abierta)
/// y el código de canje entrega la sesión una sola vez y solo a la App que lo pidió.
/// </summary>
public class S125LoginExternoCanjeServiceTests
{
    private const string UrlWeb = "https://localhost:7275";

    private static LoginExternoCanjeService CrearServicio()
    {
        var cache = new ServiceCollection()
            .AddLogging()
            .AddCbpCache(c => c.UseLocal(new MemoryCacheProvider()))
            .BuildServiceProvider()
            .GetRequiredService<ICacheService>();

        var apps = new Mock<IAppRepository>();
        apps.Setup(a => a.GetByIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<App>.Success(new App { Id = 12, Codigo = "INVENTA", UrlBase = "https://localhost:57186", Activa = true }));

        return new LoginExternoCanjeService(apps.Object, cache, Mock.Of<IAuthService>());
    }

    [Theory]
    [InlineData(null, true, "https://localhost:57186/signin-callback")]
    [InlineData("https://localhost:57186/signin-callback", true, "https://localhost:57186/signin-callback")]
    [InlineData("https://localhost:57186.evil.test/signin-callback", false, null)]
    [InlineData("http://localhost:57186/signin-callback", false, null)]
    [InlineData("https://localhost:5003/signin-callback", false, null)]
    [InlineData("https://localhost:57186/signin-callback#x", false, null)]
    [InlineData("https://evil.test/signin-callback", false, null)]
    public async Task ResolverUrlRetorno_SoloPermiteLaBaseDeLaApp(string? urlRetorno, bool permitida, string? esperada)
    {
        var servicio = CrearServicio();

        var resultado = await servicio.ResolverUrlRetornoAsync(12, urlRetorno, UrlWeb, TestContext.Current.CancellationToken);

        Assert.Equal(permitida, resultado.IsSuccess);
        if (permitida)
            Assert.Equal(esperada, resultado.Value);
        else
            Assert.Equal("URL_RETORNO_NO_PERMITIDA", resultado.Error!.Code);
    }

    [Fact]
    public async Task Canjear_CodigoDeUnSoloUso()
    {
        var servicio = CrearServicio();
        var ct = TestContext.Current.CancellationToken;
        var codigo = await servicio.EmitirCodigoAsync(new AuthResponseDto { AccessToken = "at-s125", IdUsuario = 7, IdTenant = 1 }, 12, ct);

        var primero = await servicio.CanjearAsync(codigo, 12, ct);
        var segundo = await servicio.CanjearAsync(codigo, 12, ct);

        Assert.True(primero.IsSuccess);
        Assert.Equal("at-s125", primero.Value!.AccessToken);
        Assert.True(segundo.IsFailure);
        Assert.Equal("CODIGO_INVALIDO", segundo.Error!.Code);
    }

    [Fact]
    public async Task Canjear_OtraApp_FallaYConsumeElCodigo()
    {
        var servicio = CrearServicio();
        var ct = TestContext.Current.CancellationToken;
        var codigo = await servicio.EmitirCodigoAsync(new AuthResponseDto { AccessToken = "at-s125", IdUsuario = 7, IdTenant = 1 }, 12, ct);

        var otraApp = await servicio.CanjearAsync(codigo, 13, ct);
        var despues = await servicio.CanjearAsync(codigo, 12, ct);

        Assert.Equal("CODIGO_INVALIDO", otraApp.Error!.Code);
        Assert.True(despues.IsFailure);
    }
}
