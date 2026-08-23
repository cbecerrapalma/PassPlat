using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CBP.Results;
using PassPlat.Aplicacion.OAuth;

namespace PassPlat.Aplicacion.Services;

public class GitHubIdentityProvider : IExternalIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly OAuthProviderDescriptor DescriptorInstance = new(
        Code: "GITHUB",
        Capabilities: OAuthProviderCapabilities.Pkce | OAuthProviderCapabilities.RefreshToken
                    | OAuthProviderCapabilities.UserInfo,
        SupportsJwksRotation: false,
        SupportsNonce: false,
        RequiresPkce: true,
        RequiresOfflineAccess: false,
        SupportsRefreshTokenRotation: false);

    public GitHubIdentityProvider(IHttpClientFactory httpClientFactory)
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

            var tokenResponse = await client.PostAsync(
                "https://github.com/login/oauth/access_token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = authorizationCode,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri
                }), ct);

            tokenResponse.Headers.TryGetValues("Content-Type", out var contentTypes);
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);

            string? accessToken = null;
            string? scope = null;
            if (contentTypes?.Any(c => c.Contains("json")) == true)
            {
                var json = JsonSerializer.Deserialize<GitHubTokenResponse>(body);
                accessToken = json?.AccessToken;
                scope = json?.Scope;
            }
            else
            {
                var parsed = System.Web.HttpUtility.ParseQueryString(body);
                accessToken = parsed["access_token"];
                scope = parsed["scope"];
            }

            if (string.IsNullOrEmpty(accessToken))
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al intercambiar código por token en GitHub");

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var userResponse = await client.GetAsync("https://api.github.com/user", ct);
            if (!userResponse.IsSuccessStatusCode)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Error al obtener perfil de GitHub");

            var user = await userResponse.Content.ReadFromJsonAsync<GitHubUserResult>(ct);
            if (user == null)
                return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", "Respuesta vacía del perfil de GitHub");

            string? email = user.Email;
            if (string.IsNullOrEmpty(email))
            {
                var emailsResponse = await client.GetAsync("https://api.github.com/user/emails", ct);
                if (emailsResponse.IsSuccessStatusCode)
                {
                    var emails = await emailsResponse.Content.ReadFromJsonAsync<List<GitHubEmailResult>>(ct);
                    var primary = emails?.FirstOrDefault(e => e.Primary && e.Verified);
                    email = primary?.Email;
                }
            }

            return Result<ExternalIdentityClaims>.Success(new ExternalIdentityClaims
            {
                Sub = user.Id.ToString(),
                Email = email,
                EmailVerificado = !string.IsNullOrEmpty(email),
                Nombre = user.Name ?? user.Login,
                Avatar = user.AvatarUrl,
                Scope = scope,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    login = user.Login,
                    htmlUrl = user.HtmlUrl,
                    company = user.Company,
                    location = user.Location,
                    bio = user.Bio,
                    publicRepos = user.PublicRepos
                })
            });
        }
        catch (Exception ex)
        {
            return Result<ExternalIdentityClaims>.Failure("PROVIDER_ERROR", $"Error de comunicación con GitHub: {ex.Message}");
        }
    }

    public Task<Result<TokenRefreshResult>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scopes = null, CancellationToken ct = default)
        => Task.FromResult(Result<TokenRefreshResult>.Failure("NOT_SUPPORTED", "GitHub no soporta renovación de tokens"));

    public Task<Result<string>> GenerateAuthorizationUrlAsync(string redirectUri, string clientId, string? scopes, string? state = null, string? codeChallenge = null, string? nonce = null, CancellationToken ct = default)
    {
        var scope = Uri.EscapeDataString(scopes ?? "read:user user:email");
        var redirect = Uri.EscapeDataString(redirectUri);
        var s = !string.IsNullOrEmpty(state) ? $"&state={Uri.EscapeDataString(state)}" : "";
        var url = $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={redirect}&scope={scope}{s}";
        return Task.FromResult(Result<string>.Success(url));
    }
}

internal class GitHubTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal class GitHubUserResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("public_repos")]
    public int PublicRepos { get; set; }
}

internal class GitHubEmailResult
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }
}
