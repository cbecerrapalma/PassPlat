using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests.Performance;

public class PerformanceRegressionTests
{
    [Fact]
    public async Task GenerateAuthorizationUrl_Performance()
    {
        var provider = TestHelpers.CreateProvider();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            var result = await provider.GenerateAuthorizationUrlAsync(
                "https://localhost:5001/api/auth/externo/GOOGLE/callback?v=" + i,
                "client-id-123.apps.googleusercontent.com",
                "openid email profile",
                state: Guid.NewGuid().ToString(),
                codeChallenge: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                nonce: Guid.NewGuid().ToString());
            Assert.True(result.IsSuccess);
        }

        sw.Stop();
        var avgMs = sw.Elapsed.TotalMilliseconds / 1000.0;
        Assert.True(avgMs < 10.0,
            $"GenerateAuthorizationUrlAsync avg {avgMs:F2}ms exceeds 10ms threshold");
    }

    [Fact]
    public async Task ValidateToken_Performance()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys);
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var idToken = TestHelpers.CreateSignedJwt(key,
        [
            new System.Security.Claims.Claim("sub", "perf-user"),
            new System.Security.Claims.Claim("email", "perf@test.com"),
            new System.Security.Claims.Claim("name", "Perf User"),
        ]);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = idToken, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            var result = await provider.ValidateAndExtractClaimsAsync(
                "code-" + i, "https://localhost/callback", "test-client-id", "secret",
                null, "verifier-" + i, null);
            Assert.True(result.IsSuccess);
        }

        sw.Stop();
        var avgMs = sw.Elapsed.TotalMilliseconds / 100.0;
        Assert.True(avgMs < 100.0,
            $"ValidateAndExtractClaimsAsync avg {avgMs:F2}ms exceeds 100ms threshold");
    }

    [Fact]
    public async Task RefreshToken_Performance()
    {
        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "perf-at-" + Guid.NewGuid(),
                    expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = TestHelpers.CreateProvider(httpFactory: httpFactory);

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            var result = await provider.RefreshTokenAsync(
                "rt-" + i, "client-id", "client-secret");
            Assert.True(result.IsSuccess);
        }

        sw.Stop();
        var avgMs = sw.Elapsed.TotalMilliseconds / 100.0;
        Assert.True(avgMs < 50.0,
            $"RefreshTokenAsync avg {avgMs:F2}ms exceeds 50ms threshold");
    }

    [Fact]
    public async Task DescriptorAccess_Performance()
    {
        var provider = TestHelpers.CreateProvider();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100000; i++)
        {
            var code = provider.ProviderCode;
            var supportsRefresh = provider.SupportsRefreshToken;
            var descriptor = provider.Descriptor;
        }

        sw.Stop();
        var avgUs = sw.Elapsed.TotalMicroseconds / 100000.0;
        Assert.True(avgUs < 10.0,
            $"Descriptor access avg {avgUs:F2}us exceeds 10us threshold");
    }

    [Fact]
    public async Task GoogleProvider_ConcurrentPerformance()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys);
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var idToken = TestHelpers.CreateSignedJwt(key,
        [
            new System.Security.Claims.Claim("sub", "conc-user"),
            new System.Security.Claims.Claim("email", "conc@test.com"),
        ]);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = idToken, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 50).Select(i =>
            provider.ValidateAndExtractClaimsAsync(
                "code-" + i, "https://localhost/callback", "test-client-id", "secret",
                null, "verifier-" + i, null));

        var results = await Task.WhenAll(tasks);

        sw.Stop();

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.True(sw.Elapsed.TotalSeconds < 10.0,
            $"50 concurrent validations took {sw.Elapsed.TotalSeconds:F2}s, exceeds 10s threshold");
    }
}
