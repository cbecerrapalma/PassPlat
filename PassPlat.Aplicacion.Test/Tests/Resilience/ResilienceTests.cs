using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using PassPlat.Aplicacion.Services;
using CBP.Results;

namespace PassPlat.Aplicacion.Test.Tests.Resilience;

public class ResilienceTests
{
    [Fact]
    public async Task HttpError_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error")
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code-1", "https://localhost:5001/callback",
            "client-id", "client-secret",
            null, "verifier-1", null);

        Assert.True(result.IsFailure);
        Assert.Equal("PROVIDER_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task Timeout_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandlerAsync(async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", expires_in = 3600
                }))
            };
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await provider.ValidateAndExtractClaimsAsync(
            "code-1", "https://localhost:5001/callback",
            "client-id", "client-secret",
            null, "verifier-1", null, cts.Token);

        Assert.True(result.IsFailure);
        Assert.Equal("PROVIDER_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task MalformedJson_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("not-json-at-all")
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code-1", "https://localhost:5001/callback",
            "client-id", "client-secret",
            null, "verifier-1", null);

        Assert.True(result.IsFailure);
        Assert.Equal("PROVIDER_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task NoAccessToken_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    id_token = "some-jwt", expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code-1", "https://localhost:5001/callback",
            "client-id", "client-secret",
            null, "verifier-1", null);

        Assert.True(result.IsFailure);
        Assert.Equal("NO_KEYS", result.Error!.Code);
    }

    [Fact]
    public async Task RefreshToken_NetworkError_ReturnsFailure()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(handlerMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(
            "refresh-token-1", "client-id", "client-secret");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RefreshToken_ServerError_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
            {
                Content = new StringContent("Bad Gateway")
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(
            "refresh-token-1", "client-id", "client-secret");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RefreshToken_Timeout_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandlerAsync(async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "new-at", expires_in = 3600
                }))
            };
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await provider.RefreshTokenAsync(
            "refresh-token-1", "client-id", "client-secret", null, cts.Token);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task MalformedTokenResponse_ReturnsFailure()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{{{ invalid json }}")
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(
            "refresh-token-1", "client-id", "client-secret");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task JWKS_Empty_ReturnsFailure()
    {
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<Microsoft.IdentityModel.Tokens.SecurityKey>());
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = "header.eyJzdWIiOiIxIn0.sig", expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code-1", "https://localhost:5001/callback",
            "client-id", "client-secret",
            null, "verifier-1", null);

        Assert.True(result.IsFailure);
        Assert.Equal("PROVIDER_ERROR", result.Error!.Code);
    }
}
