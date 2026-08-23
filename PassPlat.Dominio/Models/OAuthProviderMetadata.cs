namespace PassPlat.Dominio.Models;

public sealed class OAuthProviderMetadata
{
    public bool SupportsPAR { get; init; }
    public bool SupportsNonce { get; init; }
    public bool SupportsDynamicClientRegistration { get; init; }
    public IReadOnlyList<string> Claims { get; init; } = [];
}
