namespace PassPlat.Aplicacion.Services;

public class OAuthSession
{
    public string CodeVerifier { get; init; } = string.Empty;
    public string? Nonce { get; init; }
    public string ProviderCode { get; init; } = string.Empty;
    public int IdTenant { get; init; }
    public int IdApp { get; init; }
    public string? RedirectUri { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
