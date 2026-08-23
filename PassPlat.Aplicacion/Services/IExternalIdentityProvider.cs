using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class ExternalIdentityClaims
{
    public string Sub { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerificado { get; set; }
    public string? Nombre { get; set; }
    public string? Avatar { get; set; }
    public string? Scope { get; set; }
    public string? MetadataJson { get; set; }
}

public class TokenRefreshResult
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
}

public interface IExternalIdentityProvider
{
    string ProviderCode { get; }
    bool SupportsRefreshToken { get; }
    OAuthProviderDescriptor Descriptor { get; }
    Task<Result<ExternalIdentityClaims>> ValidateAndExtractClaimsAsync(string authorizationCode, string redirectUri, string clientId, string clientSecret, string? scopes, string? codeVerifier = null, string? nonce = null, CancellationToken ct = default);
    Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default);
    Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default);
}
