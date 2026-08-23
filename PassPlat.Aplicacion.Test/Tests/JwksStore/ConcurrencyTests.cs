using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Services;
using CBP.Results;

namespace PassPlat.Aplicacion.Test.Tests.JwksStore;

public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentTokenValidation_AllSucceed()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).GetSigningKeys());
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var idToken = TestHelpers.CreateSignedJwt(key,
        [
            new System.Security.Claims.Claim("sub", "conc-user"),
            new System.Security.Claims.Claim("email", "conc@test.com"),
            new System.Security.Claims.Claim("name", "Concurrent User"),
        ]);

        var callCount = 0;
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
        {
            Interlocked.Increment(ref callCount);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at-conc-" + callCount,
                    id_token = idToken,
                    expires_in = 3600
                }))
            };
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var tasks = Enumerable.Range(0, 20).Select(i =>
            provider.ValidateAndExtractClaimsAsync(
                "code-" + i, "https://localhost/callback", "test-client-id", "secret",
                null, "verifier-" + i, null));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.Equal("conc-user", r.Value!.Sub));
        Assert.Equal(20, callCount);
    }

    [Fact]
    public async Task ConcurrentCancellation_AllGraceful()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).GetSigningKeys());
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var idToken = TestHelpers.CreateSignedJwt(key,
        [
            new System.Security.Claims.Claim("sub", "cancel-user"),
        ]);

        var httpMock = TestHelpers.CreateMockHttpHandlerAsync(async (_, _) =>
        {
            await Task.Delay(5000);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = idToken, expires_in = 3600
                }))
            };
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var tasks = Enumerable.Range(0, 10).Select(i =>
            provider.ValidateAndExtractClaimsAsync(
                "code-" + i, "https://localhost/callback", "client-id", "secret",
                null, "verifier-" + i, null, cts.Token));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsFailure));
    }

    [Fact]
    public async Task ConcurrentProviderPropertyAccess_Safe()
    {
        var provider = TestHelpers.CreateProvider();

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var code = provider.ProviderCode;
            var supportsRefresh = provider.SupportsRefreshToken;
            var descriptor = provider.Descriptor;
            return (code, supportsRefresh, descriptor);
        }));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal("GOOGLE", r.code));
        Assert.All(results, r => Assert.True(r.supportsRefresh));
        Assert.All(results, r => Assert.NotNull(r.descriptor));
    }

    [Fact]
    public async Task ConcurrentMockStoreAccess_Safe()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).GetSigningKeys());
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var tasks = Enumerable.Range(0, 50).Select(_ =>
            jwksMock.Object.GetSigningKeysAsync("GOOGLE", default));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.NotEmpty(r.Value!));
    }

    [Fact]
    public async Task ConcurrentRefreshToken_Safe()
    {
        var callCount = 0;
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
        {
            Interlocked.Increment(ref callCount);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "new-at-" + callCount,
                    expires_in = 3600
                }))
            };
        });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var tasks = Enumerable.Range(0, 30).Select(i =>
            provider.RefreshTokenAsync(
                "rt-" + i, "client-id", "client-secret"));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.NotNull(r.Value));
        Assert.All(results, r => Assert.StartsWith("new-at-", r.Value!.AccessToken));
        Assert.Equal(30, callCount);
    }

    [Fact]
    public async Task ConcurrentAuthorizeUrl_Safe()
    {
        var callCount = 0;
        var provider = TestHelpers.CreateProvider();

        var tasks = Enumerable.Range(0, 50).Select(i =>
        {
            Interlocked.Increment(ref callCount);
            return provider.GenerateAuthorizationUrlAsync(
                "https://localhost/callback",
                "client-id",
                "openid email profile",
                state: Guid.NewGuid().ToString(),
                codeChallenge: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                nonce: Guid.NewGuid().ToString());
        });

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.Contains("client_id=client-id", r.Value!));
        Assert.All(results, r => Assert.Contains("code_challenge", r.Value!));
        Assert.All(results, r => Assert.Contains("state=", r.Value!));
        Assert.All(results, r => Assert.Contains("nonce=", r.Value!));
        Assert.Equal(50, callCount);
    }
}
