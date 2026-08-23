using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class LinkedInIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "LINKEDIN",
        Capabilities: OAuthProviderCapabilities.Oidc | OAuthProviderCapabilities.Pkce
                    | OAuthProviderCapabilities.UserInfo | OAuthProviderCapabilities.Nonce,
        SupportsJwksRotation: false,
        SupportsNonce: true,
        RequiresPkce: true,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public LinkedInIdentityProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
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

            var tokenResponse = await client.PostAsync(
                "https://www.linkedin.com/oauth/v2/accessToken",
                new FormUrlEncodedContent(tokenParams), ct);

            if (!tokenResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en LinkedIn");

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<LinkedInTokenResponse>(ct);
            if (tokenData?.AccessToken == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió access_token de LinkedIn");

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

            var userResponse = await client.GetAsync(
                "https://api.linkedin.com/v2/userinfo", ct);

            if (!userResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al obtener perfil de LinkedIn");

            var user = await userResponse.Content.ReadFromJsonAsync<LinkedInUserResult>(ct);
            if (user?.Sub == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Respuesta vacía del perfil de LinkedIn");

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = user.Sub,
                Email = user.Email,
                EmailVerificado = user.EmailVerified ?? false,
                Nombre = user.Name,
                Avatar = user.Picture,
                Scope = tokenData.Scope,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    tokenData.AccessToken,
                    locale = user.Locale,
                    givenName = user.GivenName,
                    familyName = user.FamilyName
                })
            });
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con LinkedIn: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "LinkedIn no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "openid profile email");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var pkce = !string.IsNullOrEmpty(codeChallenge) ? $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256" : "";
        var url = $"https://www.linkedin.com/oauth/v2/authorization?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={redirect}&scope={scope}{s}{pkce}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class LinkedInTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class LinkedInUserResult
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
}
