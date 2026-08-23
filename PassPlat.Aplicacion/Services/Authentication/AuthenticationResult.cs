using System.Security.Claims;

namespace PassPlat.Aplicacion.Services.Authentication;

public sealed record AuthenticationResult(
    string AccessToken,
    string RefreshToken,
    string Jti,
    DateTime ExpiresAt,
    Guid? SessionId,
    AuthenticationOrigin Origen);
