using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class FacebookIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string AuthorizeUrl = "https://www.facebook.com/v18.0/dialog/oauth";
    private const string TokenUrl = "https://graph.facebook.com/v18.0/oauth/access_token";
    private const string UserInfoUrl = "https://graph.facebook.com/v18.0/me?fields=id,name,email";

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "FACEBOOK",
        Capabilities: OAuthProviderCapabilities.UserInfo,
        SupportsJwksRotation: false,
        SupportsNonce: false,
        RequiresPkce: false,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public FacebookIdentityProvider(IHttpClientFactory httpClientFactory)
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
                ["redirect_uri"] = redirectUri,
                ["code"] = authorizationCode
            };

            var tokenResponse = await client.GetAsync($"{TokenUrl}?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&client_secret={Uri.EscapeDataString(clientSecret)}&code={Uri.EscapeDataString(authorizationCode)}", ct);
            if (!tokenResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en Facebook");

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<FacebookTokenResponse>(ct);
            if (tokenData?.AccessToken == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se recibió access_token de Facebook");

            var userRequest = $"{UserInfoUrl}&access_token={Uri.EscapeDataString(tokenData.AccessToken)}";
            var userResponse = await client.GetAsync(userRequest, ct);
            if (!userResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al obtener información del usuario de Facebook");

            var userData = await userResponse.Content.ReadFromJsonAsync<FacebookUserInfo>(ct);
            if (userData?.Id == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "No se pudo obtener el ID del usuario de Facebook");

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = userData.Id,
                Email = userData.Email,
                EmailVerificado = !string.IsNullOrEmpty(userData.Email),
                Nombre = userData.Name,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    tokenData.AccessToken,
                    tokenData.ExpiresIn
                })
            });
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con Facebook: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "Facebook no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "email public_profile");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var url = $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={redirect}&scope={scope}&response_type=code{s}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class FacebookTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal class FacebookUserInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
