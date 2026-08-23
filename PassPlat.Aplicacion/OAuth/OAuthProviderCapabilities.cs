namespace PassPlat.Aplicacion.OAuth;

[Flags]
public enum OAuthProviderCapabilities
{
    None = 0,
    Oidc = 1 << 0,
    Pkce = 1 << 1,
    RefreshToken = 1 << 2,
    UserInfo = 1 << 3,
    Jwks = 1 << 4,
    Revocation = 1 << 5,
    Nonce = 1 << 6,
}
