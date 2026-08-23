using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class MicrosoftIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJwksStore _jwksStore;

    private const string JwksUri = "https://login.microsoftonline.com/common/discovery/v2.0/keys";
    private const string Issuer = "https://login.microsoftonline.com/common/v2.0";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "MICROSOFT",
        Capabilities: OAuthProviderCapabilities.Oidc | OAuthProviderCapabilities.Pkce
                    | OAuthProviderCapabilities.Jwks | OAuthProviderCapabilities.UserInfo
                    | OAuthProviderCapabilities.Nonce,
        SupportsJwksRotation: true,
        SupportsNonce: true,
        RequiresPkce: true,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public MicrosoftIdentityProvider(IHttpClientFactory httpClientFactory, IJwksStore jwksStore)
    {
        _httpClientFactory = httpClientFactory;
        _jwksStore = jwksStore;
    }

    public string ProviderCode => DescriptorInstance.Code;
    public bool SupportsRefreshToken => DescriptorInstance.Capabilities.HasFlag(OAuthProviderCapabilities.RefreshToken);
    public OAuthProviderDescriptor Descriptor => DescriptorInstance;

    public async Task<Result<ExternalIdentityClaims>> ValidateAndExtractClaimsAsync(
        string authorizationCode, string redirectUri, string clientId, string clientSecret, string? scopes, string? codeVerifier = null, string? nonce = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OAuth.Token");

            var scope = scopes ?? "openid profile email User.Read";

            var tokenParams = new Dictionary<string, string>
            {
                ["code"] = authorizationCode,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = scope
            };
            if (!string.IsNullOrEmpty(codeVerifier))
                tokenParams["code_verifier"] = codeVerifier;

            var tokenResponse = await client.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                new FormUrlEncodedContent(tokenParams), ct);

            if (!tokenResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en Microsoft");

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(ct);
            if (tokenData?.IdToken == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió id_token de Microsoft");

            var handler = new JwtSecurityTokenHandler();

            var keysResult = await _jwksStore.GetSigningKeysAsync(JwksUri, ct);
            if (keysResult.IsFailure)
                return Result<ExternalIdentityClaims>.Failure(keysResult.Error!);

            var validationParameters = new TokenValidationParameters
            {
                IssuerSigningKeys = keysResult.Value,
                ValidIssuer = Issuer,
                ValidAudience = clientId,
                ClockSkew = TimeSpan.FromMinutes(5),
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true
            };

            var principal = handler.ValidateToken(tokenData.IdToken, validationParameters, out var validatedToken);
            var jwt = (JwtSecurityToken)validatedToken;

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "oid")?.Value ?? string.Empty;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == "preferred_username")?.Value;
            var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var tid = jwt.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            var tokenNonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;

            if (!string.IsNullOrEmpty(nonce) && tokenNonce != nonce)
                return Result<ExternalIdentityClaims>.Failure("NONCE_MISMATCH", "Nonce del id_token no coincide con el enviado");

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = sub,
                Email = email,
                Nombre = name,
                Scope = tokenData.Scope,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    tokenData.AccessToken,
                    tokenData.RefreshToken,
                    tokenData.ExpiresIn,
                    tokenData.IdToken,
                    tenantId = tid
                })
            });
        }
        catch (SecurityTokenExpiredException)
        {
            return Result<ExternalIdentityClaims>.Failure("TOKEN_EXPIRED", "El id_token de Microsoft ha expirado");
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            return Result<ExternalIdentityClaims>.Failure("AUD_MISMATCH", "El audience del id_token no coincide con el client_id");
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            return Result<ExternalIdentityClaims>.Failure("ISSUER_MISMATCH", "El issuer del id_token no coincide");
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            return Result<ExternalIdentityClaims>.Failure("SIGNATURE_INVALID", "La firma del id_token de Microsoft no es válida");
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con Microsoft: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "Microsoft no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "openid profile email User.Read");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var pkce = !string.IsNullOrEmpty(codeChallenge) ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256" : "";
        var n = !string.IsNullOrEmpty(nonce) ? $"&nonce={Uri.EscapeDataString(nonce)}" : "";
        var url = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={redirect}&scope={scope}{s}{pkce}{n}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class MicrosoftTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
