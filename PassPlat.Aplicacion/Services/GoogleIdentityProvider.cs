using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.OAuth;
using PassPlat.Aplicacion.Services.OAuth;

namespace PassPlat.Aplicacion.Services;

/// <summary>
/// S129 · D2 — Google solo OAuth2: canje del código y perfil por <c>/oauth2/v3/userinfo</c> con el access_token.
/// Sin id_token, JWKS, issuer ni nonce.
/// </summary>
public class GoogleIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleIdentityProvider> _logger;

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "GOOGLE",
        Capabilities: OAuthProviderCapabilities.Pkce | OAuthProviderCapabilities.RefreshToken
                    | OAuthProviderCapabilities.UserInfo,
        RequiresPkce: true,
        RequiresOfflineAccess: true,
        SupportsRefreshTokenRotation: true);

    public GoogleIdentityProvider(IHttpClientFactory httpClientFactory, ILogger<GoogleIdentityProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderCode => DescriptorInstance.Code;
    public bool SupportsRefreshToken => DescriptorInstance.Capabilities.HasFlag(OAuthProviderCapabilities.RefreshToken);
    public OAuthProviderDescriptor Descriptor => DescriptorInstance;

    public async Task<Result<ExternalIdentityClaims>> ValidateAndExtractClaimsAsync(
        string authorizationCode, string redirectUri, string clientId, string clientSecret, string? scopes, string? codeVerifier = null, CancellationToken ct = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["GoogleRedirectUri"] = redirectUri
        });

        _logger.LogInformation(OAuthEventIds.TokenRequest,
            "[A] Token exchange iniciado | ClientId={ClientId} | RedirectUri={RedirectUri} | HasCodeVerifier={HasCV}",
            clientId[..Math.Min(clientId.Length, 20)], redirectUri, !string.IsNullOrWhiteSpace(codeVerifier));

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

            // S129 · D10 — sin valores: ni code ni code_verifier en el log.
            _logger.LogInformation(OAuthEventIds.TokenRequest,
                "[A] POST a {TokenEndpoint} | Params={Params} | HasCodeVerifier={HasCV}",
                TokenEndpoint, string.Join(", ", tokenParams.Keys.Where(k => k != "client_secret")), !string.IsNullOrWhiteSpace(codeVerifier));

            var tokenResponse = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(tokenParams), ct);

            _logger.LogInformation(OAuthEventIds.TokenResponse,
                "[B] Token endpoint respondió | StatusCode={StatusCode} | Success={Success}",
                (int)tokenResponse.StatusCode, tokenResponse.IsSuccessStatusCode);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(OAuthEventIds.TokenResponseBody,
                    "[B] Token endpoint ERROR | StatusCode={StatusCode} | Body={Body}",
                    (int)tokenResponse.StatusCode, errorBody);
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error al intercambiar código por token en Google. HTTP {(int)tokenResponse.StatusCode}");
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct);
            _logger.LogInformation(OAuthEventIds.TokenResponse,
                "[C] Token response parseado | HasAccessToken={HasAT} | HasRefreshToken={HasRT} | ExpiresIn={ExpIn}",
                !string.IsNullOrEmpty(tokenData?.AccessToken), tokenData?.RefreshToken != null, tokenData?.ExpiresIn);

            if (string.IsNullOrEmpty(tokenData?.AccessToken))
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió access_token de Google");

            using var perfilRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            perfilRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            var perfilResponse = await client.SendAsync(perfilRequest, ct);

            _logger.LogInformation(OAuthEventIds.TokenClaims,
                "[D] Perfil de Google respondió | StatusCode={StatusCode}", (int)perfilResponse.StatusCode);

            if (!perfilResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error al obtener el perfil de Google. HTTP {(int)perfilResponse.StatusCode}");

            var perfil = await perfilResponse.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(ct);
            if (string.IsNullOrWhiteSpace(perfil?.Sub))
            {
                _logger.LogError(OAuthEventIds.TokenClaims, "[D] Perfil de Google sin 'sub'");
                return Result<ExternalIdentityClaims>.Failure("USERINFO_INVALIDO", "El perfil de Google no trae sub");
            }

            _logger.LogInformation(OAuthEventIds.TokenClaims,
                "[E] Claims extraídos | Sub={Sub} | EmailVerified={EmailV} | HasEmail={HasEmail} | HasAvatar={HasAvatar}",
                perfil.Sub, perfil.EmailVerified == true, !string.IsNullOrWhiteSpace(perfil.Email), !string.IsNullOrWhiteSpace(perfil.Picture));

            var metadata = new
            {
                tokenData.AccessToken,
                tokenData.RefreshToken,
                tokenData.ExpiresIn
            };

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = perfil.Sub,
                Email = perfil.Email,
                EmailVerificado = perfil.EmailVerified == true,
                Nombre = perfil.Name,
                Avatar = perfil.Picture,
                Scope = tokenData.Scope,
                MetadataJson = JsonSerializer.Serialize(metadata)
            });
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

            var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(refreshParams), ct);

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

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "email profile");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var pkce = !string.IsNullOrEmpty(codeChallenge) ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256" : "";
        var offline = "&access_type=offline&prompt=consent";
        var url = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={redirect}&scope={scope}{offline}{s}{pkce}";
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
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class GoogleUserInfoResponse
{
    [JsonPropertyName("sub")]
    public string? Sub { get; set; }
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("picture")]
    public string? Picture { get; set; }
}
