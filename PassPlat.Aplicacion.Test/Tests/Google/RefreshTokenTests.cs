using System.Text.Json;
using Moq;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests.Google;

public class RefreshTokenTests
{
    private const string ClientId = "test-client-id.apps.googleusercontent.com";
    private const string ClientSecret = "test-client-secret";
    private const string RefreshToken = "valid-refresh-token-1//abc-def";

    [Fact]
    public async Task RefreshToken_ValidResponse_ReturnsTokenRefreshResult()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "new-access-token-xyz",
                    expires_in = 3600,
                    scope = "openid email profile"
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token-xyz", result.Value!.AccessToken);
        Assert.Equal(3600, result.Value.ExpiresIn);
        Assert.Equal("openid email profile", result.Value.Scope);
    }

    [Fact]
    public async Task RefreshToken_MissingAccessToken_ReturnsRefreshError()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    expires_in = 3600,
                    scope = "openid"
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFRESH_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task RefreshToken_HttpError_ReturnsRefreshError()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFRESH_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task RefreshToken_Cancelled_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret, ct: cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFRESH_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task RefreshToken_InvalidJson_ReturnsRefreshError()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("not-valid-json{{{")
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var result = await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret);

        Assert.False(result.IsSuccess);
        Assert.Equal("REFRESH_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task RefreshToken_UsesNamedClientOAuthToken()
    {
        string? usedClientName = null;
        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var mockHandler = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "new-at",
                    expires_in = 3600
                }))
            });
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                usedClientName = name;
                return new HttpClient(mockHandler.Object);
            });

        var provider = TestHelpers.CreateProvider(httpFactory: factoryMock);

        await provider.RefreshTokenAsync(RefreshToken, ClientId, ClientSecret);

        Assert.Equal("OAuth.Token", usedClientName);
    }
}
