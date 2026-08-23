using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CBP.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests.Google;

public class SecurityTests
{
    private const string ClientId = "test-client-id";
    private const string RedirectUri = "https://localhost:5001/api/auth/externo/GOOGLE/callback";
    private const string Nonce = "secure-nonce";

    [Fact]
    public async Task AlgNone_IsRejected()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: ClientId,
            claims: [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            expires: DateTime.UtcNow.AddHours(1));
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundAlgorithmMap.Clear();
        var unsignedToken = handler.WriteToken(token);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = unsignedToken, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROVIDER_ERROR", result.Error?.Code);
    }

    [Fact]
    public async Task AlgHS256_IsRejected()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        using var hmac = new HMACSHA256();
        var hmacKey = new SymmetricSecurityKey(hmac.Key);
        var creds = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: ClientId,
            claims: [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        var hs256Token = new JwtSecurityTokenHandler().WriteToken(token);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = hs256Token, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TokenWithoutSub_IsRejected()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("email", "a@b.com")],
            nonce: Nonce);

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

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_SUB_CLAIM", result.Error?.Code);
    }

    [Fact]
    public async Task TokenWithoutExp_IsRejected()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: ClientId,
            claims: [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            signingCredentials: creds);
        var noExpToken = new JwtSecurityTokenHandler().WriteToken(token);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = noExpToken, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task MultipleAudiences_ValidWhenClientIdPresent()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: null!,
            claims:
            [
                new Claim("sub", "u1"),
                new Claim("email", "a@b.com"),
                new Claim("aud", ClientId),
                new Claim("aud", "other-client-id")
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        var multiAudToken = new JwtSecurityTokenHandler().WriteToken(token);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = multiAudToken, expires_in = 3600
                }))
            });
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        var provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("u1", result.Value!.Sub);
    }

    [Fact]
    public async Task ClockSkew_WithinLimit_Accepts()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var provider = TestHelpers.CreateProviderWithJwksAndToken(key, "test-key-1",
            nonce: Nonce, tokenNonce: Nonce,
            tokenExpired: false);
        var exp = DateTime.UtcNow.AddMinutes(-4);

        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            expires: exp, nonce: Nonce);

        var httpMock = TestHelpers.CreateMockHttpHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "at", id_token = idToken, expires_in = 3600
                }))
            });
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));
        var httpFactory = TestHelpers.CreateMockHttpClientFactory(httpMock.Object);
        provider = new GoogleIdentityProvider(httpFactory.Object, jwksMock.Object, NullLogger<GoogleIdentityProvider>.Instance);

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", Nonce);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ClockSkew_BeyondLimit_Rejects()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksKeys = new JsonWebKeySet(TestHelpers.CreateJwksJson(key)).Keys;
        var jwksMock = TestHelpers.CreateMockJwksStore(new List<SecurityKey>(jwksKeys));

        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            expires: DateTime.UtcNow.AddMinutes(-6), nonce: Nonce);

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

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", Nonce);

        Assert.False(result.IsSuccess);
        Assert.Equal("TOKEN_EXPIRED", result.Error?.Code);
    }

    [Fact]
    public async Task KidNotFound_ReturnsSignatureInvalid()
    {
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var jwksMock = TestHelpers.CreateFailedMockJwksStore("SIGNATURE_INVALID",
            "No key found for kid 'unknown-kid'");

        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")],
            nonce: Nonce);

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

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task EmptyJwks_ReturnsSignatureInvalid()
    {
        var jwksMock = TestHelpers.CreateMockJwksStore(
            Array.Empty<SecurityKey>());
        var (rsa, key) = TestHelpers.CreateRsaKey();
        var idToken = TestHelpers.CreateSignedJwt(key,
            [new Claim("sub", "u1"), new Claim("email", "a@b.com")]);

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

        var result = await provider.ValidateAndExtractClaimsAsync(
            "code", RedirectUri, ClientId, "secret", null, "verifier", null);

        Assert.False(result.IsSuccess);
        Assert.Equal("SIGNATURE_INVALID", result.Error?.Code);
    }
}
