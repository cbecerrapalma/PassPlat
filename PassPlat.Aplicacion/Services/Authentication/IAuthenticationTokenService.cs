using CBP.Results;

namespace PassPlat.Aplicacion.Services.Authentication;

public interface IAuthenticationTokenService
{
    Task<Result<AuthenticationResult>> LoginAsync(AuthenticationContext context, CancellationToken ct = default);
    Task<Result<AuthenticationResult>> OAuthAsync(AuthenticationContext context, CancellationToken ct = default);
    Task<Result<AuthenticationResult>> RefreshAsync(AuthenticationContext context, string? oldRefreshHash, Guid? sessionId, CancellationToken ct = default);
}
