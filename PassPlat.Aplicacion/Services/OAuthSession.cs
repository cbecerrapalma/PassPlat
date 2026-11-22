namespace PassPlat.Aplicacion.Services;

public class OAuthSession
{
    public string CodeVerifier { get; init; } = string.Empty;
    public string ProviderCode { get; init; } = string.Empty;
    public int IdTenant { get; init; }
    public int IdApp { get; init; }
    public string? RedirectUri { get; init; }
    /// <summary>S125 · D1 — URL de la app a la que vuelve el login (ya validada contra Apps.UrlBase).</summary>
    public string? UrlRetorno { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
