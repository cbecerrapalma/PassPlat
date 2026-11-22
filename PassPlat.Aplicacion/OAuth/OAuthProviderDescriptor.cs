namespace PassPlat.Aplicacion.OAuth;

public sealed record OAuthProviderDescriptor(
    string Code,
    OAuthProviderCapabilities Capabilities,
    bool RequiresPkce,
    bool RequiresOfflineAccess,
    bool SupportsRefreshTokenRotation);
