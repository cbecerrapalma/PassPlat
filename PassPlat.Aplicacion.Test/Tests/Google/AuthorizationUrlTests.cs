using System.Web;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Services;

namespace PassPlat.Aplicacion.Test.Tests.Google;

public class AuthorizationUrlTests
{
    private readonly GoogleIdentityProvider _provider;

    public AuthorizationUrlTests()
    {
        _provider = TestHelpers.CreateProvider();
    }

    [Fact]
    public async Task GenerateUrl_ContainsAllRequiredParams()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost:5001/api/auth/externo/GOOGLE/callback",
            "test-client-id-123.apps.googleusercontent.com",
            "openid email profile",
            state: "test-state-abc",
            codeChallenge: "test-code-challenge-sha256",
            nonce: "test-nonce-xyz");

        Assert.True(result.IsSuccess);
        var url = result.Value!;
        var uri = new Uri(url);
        var qs = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("accounts.google.com", uri.Host);
        Assert.Equal("/o/oauth2/v2/auth", uri.AbsolutePath);

        Assert.Equal("code", qs["response_type"]);
        Assert.Equal("test-client-id-123.apps.googleusercontent.com", qs["client_id"]);
        Assert.Equal("openid email profile", qs["scope"]);
        Assert.Equal("test-state-abc", qs["state"]);
        Assert.Equal("S256", qs["code_challenge_method"]);
        Assert.Equal("test-code-challenge-sha256", qs["code_challenge"]);
        Assert.Equal("test-nonce-xyz", qs["nonce"]);
        Assert.Equal("offline", qs["access_type"]);
        Assert.Equal("consent", qs["prompt"]);
    }

    [Fact]
    public async Task GenerateUrl_EncodesRedirectUri()
    {
        var redirect = "https://localhost:5001/api/auth/externo/GOOGLE/callback?extra=1";
        var result = await _provider.GenerateAuthorizationUrlAsync(
            redirect, "client-id", null, state: "s", codeChallenge: "cc");

        Assert.True(result.IsSuccess);
        var uri = new Uri(result.Value!);

        var decodedRedirect = HttpUtility.ParseQueryString(uri.Query)["redirect_uri"];
        Assert.Equal(redirect, decodedRedirect);
    }

    [Fact]
    public async Task GenerateUrl_OmittedOptionalParams_DoNotAppear()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", null);

        Assert.True(result.IsSuccess);
        var uri = new Uri(result.Value!);
        var qs = HttpUtility.ParseQueryString(uri.Query);

        Assert.True(string.IsNullOrEmpty(qs["state"]), "State should not appear when omitted");
        Assert.Null(qs["code_challenge"]);
        Assert.Null(qs["code_challenge_method"]);
        Assert.Null(qs["nonce"]);
    }

    [Fact]
    public async Task GenerateUrl_AlwaysIncludesOfflineAccess()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", null);

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);

        Assert.Equal("offline", qs["access_type"]);
        Assert.Equal("consent", qs["prompt"]);
    }

    [Fact]
    public async Task GenerateUrl_DefaultScopeWhenNull()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", null);

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);
        Assert.Equal("openid email profile", qs["scope"]);
    }

    [Fact]
    public async Task GenerateUrl_CustomScope()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", "openid email profile https://www.googleapis.com/auth/drive.readonly");

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);
        Assert.Contains("https://www.googleapis.com/auth/drive.readonly", qs["scope"]!);
    }

    [Fact]
    public async Task GenerateUrl_StateEncoded()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", null,
            state: "state+with special&chars=123");

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);
        Assert.Equal("state+with special&chars=123", qs["state"]);
    }

    [Fact]
    public async Task GenerateUrl_NonceEncoded()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback", "client-id", null,
            state: "s", codeChallenge: "cc", nonce: "nonce+with/chars=");

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);
        Assert.Equal("nonce+with/chars=", qs["nonce"]);
    }

    [Fact]
    public async Task GenerateUrl_ClientIdEncoded()
    {
        var result = await _provider.GenerateAuthorizationUrlAsync(
            "https://localhost/callback",
            "1234567890-abc123def456.apps.googleusercontent.com",
            null);

        Assert.True(result.IsSuccess);
        var qs = HttpUtility.ParseQueryString(new Uri(result.Value!).Query);
        Assert.Equal("1234567890-abc123def456.apps.googleusercontent.com", qs["client_id"]);
    }

    [Fact]
    public void Descriptor_HasCorrectCapabilities()
    {
        var descriptor = _provider.Descriptor;

        Assert.Equal("GOOGLE", descriptor.Code);
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.Oidc));
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.Pkce));
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.RefreshToken));
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.Jwks));
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.UserInfo));
        Assert.True(descriptor.Capabilities.HasFlag(OAuthProviderCapabilities.Nonce));
        Assert.True(descriptor.RequiresPkce);
        Assert.True(descriptor.SupportsNonce);
        Assert.True(descriptor.SupportsJwksRotation);
        Assert.True(descriptor.RequiresOfflineAccess);
        Assert.True(descriptor.SupportsRefreshTokenRotation);
    }
}
