using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Services.OAuth;

namespace PassPlat.Aplicacion.Services;

public class GoogleIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJwksStore _jwksStore;
    private readonly ILogger<GoogleIdentityProvider> _logger;

    private const string JwksUri = "https://www.googleapis.com/oauth2/v3/certs";
    private const string Issuer = "https://accounts.google.com";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "GOOGLE",
        Capabilities: OAuthProviderCapabilities.Oidc | OAuthProviderCapabilities.Pkce
                    | OAuthProviderCapabilities.RefreshToken | OAuthProviderCapabilities.Jwks
                    | OAuthProviderCapabilities.UserInfo | OAuthProviderCapabilities.Nonce,
        SupportsJwksRotation: true,
        SupportsNonce: true,
        RequiresPkce: true,
        RequiresOfflineAccess: true,
        SupportsRefreshTokenRotation: true);

    public GoogleIdentityProvider(IHttpClientFactory httpClientFactory, IJwksStore jwksStore, ILogger<GoogleIdentityProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _jwksStore = jwksStore;
        _logger = logger;
    }

    public string ProviderCode => DescriptorInstance.Code;
    public bool SupportsRefreshToken => DescriptorInstance.Capabilities.HasFlag(OAuthProviderCapabilities.RefreshToken);
    public OAuthProviderDescriptor Descriptor => DescriptorInstance;

    public async Task<Result<ExternalIdentityClaims>> ValidateAndExtractClaimsAsync(
        string authorizationCode, string redirectUri, string clientId, string clientSecret, string? scopes, string? codeVerifier = null, string? nonce = null, CancellationToken ct = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["GoogleAuthzCode"] = authorizationCode[..Math.Min(authorizationCode.Length, 12)],
            ["GoogleRedirectUri"] = redirectUri
        });

        _logger.LogInformation(OAuthEventIds.TokenRequest,
            "[A] Token exchange iniciado | ClientId={ClientId} | RedirectUri={RedirectUri} | HasCodeVerifier={HasCV} | HasNonce={HasNonce}",
            clientId[..Math.Min(clientId.Length, 20)], redirectUri, !string.IsNullOrWhiteSpace(codeVerifier), !string.IsNullOrWhiteSpace(nonce));

        try
        {
            var client = _httpClientFactory.CreateClient("OAuth.Token");

            var tokenParams = new Dictionary<string, string>
            {
                ["code"] = authorizationCode,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };
            if (!string.IsNullOrEmpty(codeVerifier))
                tokenParams["code_verifier"] = codeVerifier;

            _logger.LogInformation(OAuthEventIds.TokenRequest,
                "[A] POST a https://oauth2.googleapis.com/token | Params={Params}",
                string.Join(", ", tokenParams.Where(k => k.Key != "client_secret").Select(k => $"{k.Key}={k.Value}")));

            var tokenResponse = await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenParams), ct);

            _logger.LogInformation(OAuthEventIds.TokenResponse,
                "[B] Token endpoint respondió | StatusCode={StatusCode} | Success={Success}",
                (int)tokenResponse.StatusCode, tokenResponse.IsSuccessStatusCode);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(OAuthEventIds.TokenResponseBody,
                    "[B] Token endpoint ERROR | StatusCode={StatusCode} | Body={Body}",
                    (int)tokenResponse.StatusCode, errorBody);
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error al intercambiar código por token en Google. HTTP {(int)tokenResponse.StatusCode}: {errorBody}");
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct);
            _logger.LogInformation(OAuthEventIds.TokenResponse,
                "[C] Token response parseado | HasIdToken={HasIdToken} | HasAccessToken={HasAT} | HasRefreshToken={HasRT} | ExpiresIn={ExpIn}",
                tokenData?.IdToken != null, tokenData?.AccessToken != null, tokenData?.RefreshToken != null, tokenData?.ExpiresIn);

            if (tokenData?.IdToken == null)
            {
                _logger.LogError(OAuthEventIds.TokenResponseBody,
                    "[C] No se recibió id_token de Google | AccessToken={AT}", tokenData?.AccessToken != null ? "presente" : "ausente");
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió id_token de Google");
            }

            _logger.LogInformation(OAuthEventIds.TokenJwksFetch,
                "[D] Obteniendo JWKS desde {JwksUri}", JwksUri);
            var handler = new JwtSecurityTokenHandler();

            var keysResult = await _jwksStore.GetSigningKeysAsync(JwksUri, ct);
            if (keysResult.IsFailure)
            {
                _logger.LogError(OAuthEventIds.TokenJwksResult,
                    "[D] Error obteniendo JWKS | Code={ErrorCode}", keysResult.Error?.Code);
                return Result<ExternalIdentityClaims>.Failure(keysResult.Error!);
            }

            _logger.LogInformation(OAuthEventIds.TokenJwksResult,
                "[D] JWKS obtenido | KeyCount={Count}", keysResult.Value.Count);

            _logger.LogInformation(OAuthEventIds.TokenValidation,
                "[E] Validando id_token | Issuer={Issuer} | Audience={Audience} | ClockSkew=5min",
                Issuer, clientId[..Math.Min(clientId.Length, 20)]);

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

            _logger.LogInformation(OAuthEventIds.TokenValidation,
                "[E] id_token válido | Claims={Count}", jwt.Claims.Count());

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogError(OAuthEventIds.TokenClaims, "[F] id_token sin claim 'sub'");
                return Result<ExternalIdentityClaims>.Failure("MISSING_SUB_CLAIM", "id_token no contiene el claim obligatorio 'sub'");
            }

            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var emailVerified = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value == "true";
            var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var picture = jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;
            var tokenNonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;

            _logger.LogInformation(OAuthEventIds.TokenClaims,
                "[F] Claims extraídos | Sub={Sub} | Email={Email} | EmailVerified={EmailV} | Name={Name} | HasAvatar={HasAvatar} | TokenNonce={TokenNonce}",
                sub, email ?? "(sin email)", emailVerified, name ?? "(sin nombre)", !string.IsNullOrWhiteSpace(picture), tokenNonce ?? "(sin nonce)");

            if (!string.IsNullOrEmpty(nonce))
            {
                _logger.LogInformation(OAuthEventIds.TokenNonceCheck,
                    "[G] Verificando nonce | ExpectedNonce={Expected} | TokenNonce={TokenNonce}",
                    nonce[..Math.Min(nonce.Length, 12)], tokenNonce?[..Math.Min(tokenNonce?.Length ?? 0, 12)] ?? "(null)");
                if (tokenNonce != nonce)
                {
                    _logger.LogError(OAuthEventIds.TokenNonceCheck, "[G] NONCE_MISMATCH");
                    return Result<ExternalIdentityClaims>.Failure("NONCE_MISMATCH", "Nonce del id_token no coincide con el enviado");
                }
                _logger.LogInformation(OAuthEventIds.TokenNonceCheck, "[G] Nonce OK");
            }

            var metadata = new
            {
                tokenData.AccessToken,
                tokenData.RefreshToken,
                tokenData.ExpiresIn,
                tokenData.IdToken
            };

            var scope = tokenData.Scope;

            _logger.LogInformation(OAuthEventIds.TokenClaims,
                "[H] Claims procesados OK | MetadataJson len={Len}", JsonSerializer.Serialize(metadata).Length);

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = sub,
                Email = email,
                EmailVerificado = emailVerified,
                Nombre = name,
                Avatar = picture,
                Scope = scope,
                MetadataJson = JsonSerializer.Serialize(metadata)
            });
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogError(OAuthEventIds.TokenValidation, "Id_token expirado");
            return Result<ExternalIdentityClaims>.Failure("TOKEN_EXPIRED", "El id_token de Google ha expirado");
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            _logger.LogError(OAuthEventIds.TokenValidation, "Audience mismatch | Expected={Audience}", clientId);
            return Result<ExternalIdentityClaims>.Failure("AUD_MISMATCH", "El audience del id_token no coincide con el client_id");
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            _logger.LogError(OAuthEventIds.TokenValidation, "Issuer mismatch | Expected={Issuer}", Issuer);
            return Result<ExternalIdentityClaims>.Failure("ISSUER_MISMATCH", "El issuer del id_token no coincide");
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            _logger.LogError(OAuthEventIds.TokenValidation, "Signature key no encontrada en JWKS");
            return Result<ExternalIdentityClaims>.Failure("SIGNATURE_INVALID", "La firma del id_token de Google no es válida");
        }
        catch (Exception ex)
        {
            _logger.LogError(OAuthEventIds.TokenResponse, ex, "Error de comunicación con Google");
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con Google: {ex.Message}");
        }
    }

    public async Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OAuth.Token");

            var refreshParams = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var response = await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(refreshParams), ct);

            if (!response.IsSuccessStatusCode)
                return Result<TokenRefreshResult>.Failure("REFRESH_ERROR", "Error al renovar token en Google");

            var data = await response.Content.ReadFromJsonAsync<GoogleRefreshResponse>(ct);
            if (data?.AccessToken == null)
                return Result<TokenRefreshResult>.Failure("REFRESH_ERROR", "No se recibió access_token renovado de Google");

            return Result<TokenRefreshResult>.Success(new TokenRefreshResult
            {
                AccessToken = data.AccessToken,
                ExpiresIn = data.ExpiresIn,
                Scope = data.Scope
            });
        }
        catch (Exception ex)
        {
            return Result<TokenRefreshResult>.Failure("REFRESH_ERROR", $"Error al renovar token con Google: {ex.Message}");
        }
    }

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "openid email profile");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var pkce = !string.IsNullOrEmpty(codeChallenge) ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256" : "";
        var n = !string.IsNullOrEmpty(nonce) ? $"&nonce={Uri.EscapeDataString(nonce)}" : "";
        var offline = "&access_type=offline&prompt=consent";
        var url = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={redirect}&scope={scope}{offline}{s}{pkce}{n}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class GoogleRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class GoogleTokenResponse
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
