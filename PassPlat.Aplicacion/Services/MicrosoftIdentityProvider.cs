using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

/// <summary>
/// S129 · D3 — Microsoft solo OAuth2: canje del código y perfil por Microsoft Graph <c>/v1.0/me</c> con el access_token.
/// Graph no asegura que el correo esté verificado: <see cref="ExternalIdentityClaims.EmailVerificado"/> es siempre <c>false</c>.
/// </summary>
public class MicrosoftIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string PerfilEndpoint = "https://graph.microsoft.com/v1.0/me?$select=id,displayName,mail,userPrincipalName";
    private const string ScopePorDefecto = "User.Read offline_access";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "MICROSOFT",
        Capabilities: OAuthProviderCapabilities.Pkce | OAuthProviderCapabilities.UserInfo,
        RequiresPkce: true,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public MicrosoftIdentityProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderCode => DescriptorInstance.Code;
    public bool SupportsRefreshToken => DescriptorInstance.Capabilities.HasFlag(OAuthProviderCapabilities.RefreshToken);
    public OAuthProviderDescriptor Descriptor => DescriptorInstance;

    public async Task<Result<ExternalIdentityClaims>> ValidateAndExtractClaimsAsync(
        string authorizationCode, string redirectUri, string clientId, string clientSecret, string? scopes, string? codeVerifier = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("OAuth.Token");

            var tokenParams = new Dictionary<string, string>
            {
                ["code"] = authorizationCode,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = scopes ?? ScopePorDefecto
            };
            if (!string.IsNullOrEmpty(codeVerifier))
                tokenParams["code_verifier"] = codeVerifier;

            var tokenResponse = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(tokenParams), ct);
            if (!tokenResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en Microsoft");

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(ct);
            if (string.IsNullOrEmpty(tokenData?.AccessToken))
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió access_token de Microsoft");

            using var perfilRequest = new HttpRequestMessage(HttpMethod.Get, PerfilEndpoint);
            perfilRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            var perfilResponse = await client.SendAsync(perfilRequest, ct);
            if (!perfilResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error al obtener el perfil de Microsoft. HTTP {(int)perfilResponse.StatusCode}");

            var perfil = await perfilResponse.Content.ReadFromJsonAsync<MicrosoftGraphUserResponse>(ct);
            if (string.IsNullOrWhiteSpace(perfil?.Id))
                return Result<ExternalIdentityClaims>.Failure("USERINFO_INVALIDO", "El perfil de Microsoft no trae id");

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = perfil.Id,
                Email = perfil.Mail ?? perfil.UserPrincipalName,
                EmailVerificado = false,
                Nombre = perfil.DisplayName,
                Scope = tokenData.Scope,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    tokenData.AccessToken,
                    tokenData.RefreshToken,
                    tokenData.ExpiresIn
                })
            });
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con Microsoft: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "Microsoft no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? ScopePorDefecto);
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var pkce = !string.IsNullOrEmpty(codeChallenge) ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256" : "";
        var url = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={redirect}&scope={scope}{s}{pkce}";
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

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class MicrosoftGraphUserResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }
}
