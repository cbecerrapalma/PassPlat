using System.Security.Claims;
using System.Text.Json;
using System.Web;
using CBP.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests.Google;

public class TokenValidationTests
{
    private const string ClientId = "test-client-id";
    private const string RedirectUri = "https://localhost:5001/api/auth/externo/GOOGLE/callback";
    private const string CodeVerifier = "test-code-verifier-32-chars-minimum-length";
    private const string Nonce = "test-nonce-value";
    private const string AuthCode = "valid-auth-code-123";

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsClaims()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1", nonce: Nonce, tokenNonce: Nonce);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.True(result.IsSuccess);
        var claims = result.Value!;
        Assert.Equal("test-user-123", claims.Sub);
        Assert.Equal("test@example.com", claims.Email);
        Assert.True(claims.EmailVerificado);
        Assert.Equal("Test User", claims.Nombre);
        Assert.NotNull(claims.MetadataJson);

        var metadata = JsonSerializer.Deserialize<JsonElement>(claims.MetadataJson);
        Assert.True(metadata.TryGetProperty("AccessToken", out _));
        Assert.True(metadata.TryGetProperty("RefreshToken", out _));
        Assert.True(metadata.TryGetProperty("IdToken", out _));
    }

    [Fact]
    public async Task ValidateToken_NullNonce_DoesNotValidateNonce()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1", nonce: null, tokenNonce: "some-nonce");

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateToken_NonceMismatch_ReturnsNonceMismatch()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1", nonce: "expected-nonce", tokenNonce: "different-nonce");

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, "expected-nonce");

        Assert.False(result.IsSuccess);
        Assert.Equal("NONCE_MISMATCH", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_ExpiredToken_ReturnsTokenExpired()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1",
            nonce: Nonce, tokenNonce: Nonce, tokenExpired: true);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("TOKEN_EXPIRED", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_InvalidIssuer_ReturnsIssuerMismatch()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1",
            nonce: Nonce, tokenNonce: Nonce, issuerOverride: "https://evil.com");

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("ISSUER_MISMATCH", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_InvalidAudience_ReturnsAudMismatch()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1",
            nonce: Nonce, tokenNonce: Nonce, audienceOverride: "wrong-client-id");

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUD_MISMATCH", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_InvalidSignature_ReturnsSignatureInvalid()
    {
        var (rsaJwks, keyJwks) = TestHelpers.CreateRsaKey();
        var (_, keySigning) = TestHelpers.CreateRsaKey();
        var jwksKeys = new List<SecurityKey>(new JsonWebKeySet(TestHelpers.CreateJwksJson(keyJwks, "jwks-key")).Keys);
        var jwksMock = TestHelpers.CreateMockJwksStore(jwksKeys);

        var idToken = TestHelpers.CreateSignedJwt(keySigning,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            nonce: Nonce);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at",
                    id_token = idToken,
                    expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("SIGNATURE_INVALID", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_HttpError_ReturnsProviderError()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var httpMock = TestHelpers.CreateMockHttpHandler(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway));

        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROVIDER_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_MissingIdToken_ReturnsProviderError()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var httpMock = TestHelpers.CreateMockHttpHandler(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "mock-at",
                    expires_in = 3600
                }))
            });

        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROVIDER_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_JwksFailure_PropagatesError()
    {
        var jwksMock = TestHelpers.CreateFailedMockJwksStore("JWKS_DOWN", "JWKS endpoint unavailable");
        var httpMock = TestHelpers.CreateMockHttpHandler(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "mock-at",
                    id_token = "some-token",
                    expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("JWKS_DOWN", result.Error?.Code);
    }

    [Fact]
    public async Task ValidateToken_ExtractsAllClaims()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var extraClaims = new List<Claim>
        {
            new("given_name", "Test"),
            new("family_name", "User"),
            new("locale", "en"),
            new("hd", "example.com")
        };
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1",
            nonce: Nonce, tokenNonce: Nonce, extraClaims: extraClaims);

        var result = await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.True(result.IsSuccess);
        var claims = result.Value!;
        Assert.Equal("test-user-123", claims.Sub);
        Assert.Equal("test@example.com", claims.Email);
        Assert.True(claims.EmailVerificado);
        Assert.Equal("Test User", claims.Nombre);
    }

    [Fact]
    public async Task ValidateToken_SendsCodeVerifierInTokenRequest()
    {
        string? sentCodeVerifier = null;
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            nonce: Nonce);

        var httpMock = TestHelpers.CreateMockHttpHandler(req =>
        {
            var body = req.Content?.ReadAsStringAsync().Result;
            if (body != null)
            {
                var parsed = HttpUtility.ParseQueryString(body);
                sentCodeVerifier = parsed["code_verifier"];
            }
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

        await provider.ValidateAndExtractClaimsAsync(
            AuthCode, RedirectUri, ClientId, "client-secret", null, CodeVerifier, Nonce);

        Assert.Equal(CodeVerifier, sentCodeVerifier);
    }

    [Fact]
    public async Task ValidateToken_ProviderCode_IsGoole()
    {
        var provider = TestHelpers.CreateProvider();
        Assert.Equal("GOOGLE", provider.ProviderCode);
        Assert.True(provider.SupportsRefreshToken);
    }
}
