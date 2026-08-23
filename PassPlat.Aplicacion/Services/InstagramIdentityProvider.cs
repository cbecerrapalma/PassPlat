using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class InstagramIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string AuthorizeUrl = "https://api.instagram.com/oauth/authorize";
    private const string TokenUrl = "https://api.instagram.com/oauth/access_token";
    private const string UserInfoUrl = "https://graph.instagram.com/me?fields=id,username,name,email";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "INSTAGRAM",
        Capabilities: OAuthProviderCapabilities.UserInfo,
        SupportsJwksRotation: false,
        SupportsNonce: false,
        RequiresPkce: false,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public InstagramIdentityProvider(IHttpClientFactory httpClientFactory)
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
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code"] = authorizationCode
            };

            var tokenResponse = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(tokenParams), ct);
            if (!tokenResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en Instagram");

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<InstagramTokenResponse>(ct);
            if (tokenData?.AccessToken == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió access_token de Instagram");

            var userRequest = $"https://graph.instagram.com/me?fields=id,username,name,email&access_token={Uri.EscapeDataString(tokenData.AccessToken)}";
            var userResponse = await client.GetAsync(userRequest, ct);
            if (!userResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al obtener información del usuario de Instagram");

            var userData = await userResponse.Content.ReadFromJsonAsync<InstagramUserInfo>(ct);
            if (userData?.Id == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se pudo obtener el ID del usuario de Instagram");

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = userData.Id,
                Email = userData.Email,
                EmailVerificado = !string.IsNullOrEmpty(userData.Email),
                Nombre = userData.Name ?? userData.Username,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    tokenData.AccessToken,
                    username = userData.Username
                })
            });
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con Instagram: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "Instagram no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "user_profile email");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var url = $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={redirect}&scope={scope}&response_type=code{s}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class InstagramTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }
}

internal class InstagramUserInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
