using System.Text.Json;
using CBP.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Tests.Tests;

public static class TestHelpers
{
    public static Mock<HttpMessageHandler> CreateMockHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => handler(req));
        return mock;
    }

    public static Mock<HttpMessageHandler> CreateMockHttpHandlerAsync(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) => handler(req, ct).Result);
        return mock;
    }

    public static Mock<IHttpClientFactory> CreateMockHttpClientFactory(
        HttpMessageHandler handler, string clientName = "OAuth.Token")
    {
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(f => f.CreateClient(clientName)).Returns(client);
        return factory;
    }

    public static GoogleIdentityProvider CreateProvider(
        Mock<IHttpClientFactory>? httpFactory = null)
    {
        var defaultHttpFactory = CreateMockHttpClientFactory(
            CreateMockHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "mock-at",
                    refresh_token = "mock-rt",
                    expires_in = 3600
                }))
            }).Object);
        var usedHttp = httpFactory ?? defaultHttpFactory;
        return new GoogleIdentityProvider(usedHttp.Object, NullLogger<GoogleIdentityProvider>.Instance);
    }

    public static async Task AssertResultFailure(Result result, string expectedCode)
    {
        await Task.CompletedTask;
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
    }

    public static async Task AssertResultSuccess<T>(Result<T> result, Action<T>? assertions = null)
    {
        await Task.CompletedTask;
        Assert.True(result.IsSuccess);
        if (assertions != null)
            assertions(result.Value!);
    }
}

