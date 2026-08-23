using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CBP.Results;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests;

public static class TestHelpers
{
    public static (RSA Rsa, RsaSecurityKey Key) CreateRsaKey(int keySize = 2048)
    {
        var rsa = RSA.Create(keySize);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key-1" };
        return (rsa, key);
    }

    public static string CreateJwksJson(RsaSecurityKey key, string kid = "test-key-1")
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Kid = kid;
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        jwk.Use = "sig";
        return JsonSerializer.Serialize(new { keys = new[] { jwk } });
    }

    public static string CreateSignedJwt(RsaSecurityKey key, IEnumerable<Claim> claims,
        string issuer = "https://accounts.google.com", string audience = "test-client-id",
        DateTime? expires = null, string? nonce = null)
    {
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var exp = expires ?? DateTime.UtcNow.AddHours(1);
        var allClaims = new List<Claim>(claims);
        if (nonce != null)
            allClaims.Add(new Claim("nonce", nonce));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: allClaims,
            expires: exp,
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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

    public static Mock<IJwksStore> CreateMockJwksStore(
        ICollection<SecurityKey>? keys = null)
    {
        var mock = new Mock<IJwksStore>(MockBehavior.Strict);
        if (keys != null)
        {
            mock.Setup(j => j.GetSigningKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ICollection<SecurityKey>>.Success(keys));
        }
        else
        {
            mock.Setup(j => j.GetSigningKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ICollection<SecurityKey>>.Failure("NO_KEYS", "No keys configured"));
        }
        return mock;
    }

    public static GoogleIdentityProvider CreateProvider(
        Mock<IHttpClientFactory>? httpFactory = null,
        Mock<IJwksStore>? jwksStore = null)
    {
        var defaultHttpFactory = CreateMockHttpClientFactory(
            CreateMockHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "mock-at",
                    refresh_token = "mock-rt",
                    expires_in = 3600,
                    id_token = "mock-id-token"
                }))
            }).Object);
        var defaultJwks = CreateMockJwksStore();
        var usedHttp = httpFactory ?? defaultHttpFactory;
        var usedJwks = jwksStore ?? defaultJwks;
        return new GoogleIdentityProvider(usedHttp.Object, usedJwks.Object, NullLogger<GoogleIdentityProvider>.Instance);
    }

    public static Mock<IJwksStore> CreateFailedMockJwksStore(string code, string message)
    {
        var mock = new Mock<IJwksStore>(MockBehavior.Strict);
        mock.Setup(j => j.GetSigningKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ICollection<SecurityKey>>.Failure(code, message));
        return mock;
    }

    public static GoogleIdentityProvider CreateProviderWithJwksAndToken(
        RsaSecurityKey rsaKey, string kid, string? nonce = null, string? tokenNonce = null,
        bool tokenExpired = false, string issuerOverride = "https://accounts.google.com",
        string audienceOverride = "test-client-id",
        IEnumerable<Claim>? extraClaims = null)
    {
        var jwksJson = CreateJwksJson(rsaKey, kid);
        var keyFromJwks = new JsonWebKeySet(jwksJson).GetSigningKeys();
        var jwksMock = CreateMockJwksStore(new List<SecurityKey>(keyFromJwks));

        var claims = new List<Claim>
        {
            new("sub", "test-user-123"),
            new("email", "test@example.com"),
            new("email_verified", "true"),
            new("name", "Test User")
        };
        if (extraClaims != null) claims.AddRange(extraClaims);

        var exp = tokenExpired ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow.AddHours(1);
        var idToken = CreateSignedJwt(rsaKey, claims,
            issuer: issuerOverride,
            audience: audienceOverride,
            expires: exp,
            nonce: tokenNonce ?? nonce);

        var tokenResponse = new
        {
            access_token = "mock-at-" + Guid.NewGuid(),
            refresh_token = "mock-rt-" + Guid.NewGuid(),
            expires_in = 3600,
            id_token = idToken
        };

        var httpMock = CreateMockHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
        });

        var httpFactory = CreateMockHttpClientFactory(httpMock.Object);

        return new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);
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
