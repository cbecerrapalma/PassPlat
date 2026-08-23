namespace PassPlat.Aplicacion.Services.Authentication;

public sealed record AuthenticationTokenGenerationResult(
    string AccessToken,
    string RefreshToken,
    string RefreshHash,
    string Jti,
    DateTime ExpiresAt);
