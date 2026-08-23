using System.Security.Claims;
using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Services.Authentication.Claims;

namespace PassPlat.Aplicacion.Services.Authentication;

public class AuthenticationTokenService : IAuthenticationTokenService
{
    private readonly IPermissionClaimBuilder _claimBuilder;
    private readonly AuthenticationTokenIssuer _tokenIssuer;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<AuthenticationTokenService> _logger;

    public AuthenticationTokenService(
        IPermissionClaimBuilder claimBuilder,
        AuthenticationTokenIssuer tokenIssuer,
        SessionManager sessionManager,
        ILogger<AuthenticationTokenService> logger)
    {
        _claimBuilder = claimBuilder;
        _tokenIssuer = tokenIssuer;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResult>> LoginAsync(AuthenticationContext context, CancellationToken ct)
    {
        var tokenResult = await EmitirTokensYCrearSesionAsync(context, ct);
        if (tokenResult.IsFailure)
            return Result<AuthenticationResult>.Failure(tokenResult.Error!);

        return Result<AuthenticationResult>.Success(tokenResult.Value);
    }

    public async Task<Result<AuthenticationResult>> OAuthAsync(AuthenticationContext context, CancellationToken ct)
    {
        var tokenResult = await EmitirTokensYCrearSesionAsync(context, ct);
        if (tokenResult.IsFailure)
            return Result<AuthenticationResult>.Failure(tokenResult.Error!);

        return Result<AuthenticationResult>.Success(tokenResult.Value);
    }

    public async Task<Result<AuthenticationResult>> RefreshAsync(
        AuthenticationContext context, string? oldRefreshHash, Guid? sessionId, CancellationToken ct)
    {
        // 1. Obtener permisos
        var permisoResult = await _claimBuilder.BuildPermissionClaimsAsync(context, ct);
        if (permisoResult.IsFailure)
            return Result<AuthenticationResult>.Failure(permisoResult.Error!);

        // 2. Generar nuevos tokens (no crea sesión nueva)
        var genResult = _tokenIssuer.Generate(context, permisoResult.Value);

        // 3. Rotar refresh token en la sesión existente
        if (sessionId.HasValue && oldRefreshHash != null)
        {
            var rotateResult = await _sessionManager.RotateRefreshTokenAsync(
                sessionId.Value, oldRefreshHash, genResult.RefreshHash, genResult.ExpiresAt, ct);
            if (rotateResult.IsFailure)
                return Result<AuthenticationResult>.Failure(rotateResult.Error!);
        }

        // 4. Telemetría
        _logger.LogInformation(AuthenticationEvents.TokenGenerated,
            "Refresh completado | Usuario={IdUsuario} Tenant={IdTenant} App={IdApp} Permisos={Count} SessionId={SessionId} Jti={Jti}",
            context.IdUsuario, context.IdTenant, context.IdApp,
            permisoResult.Value.Count, sessionId, genResult.Jti);

        return Result<AuthenticationResult>.Success(new AuthenticationResult(
            genResult.AccessToken, genResult.RefreshToken,
            genResult.Jti, genResult.ExpiresAt, sessionId, context.Origen));
    }

    private async Task<Result<AuthenticationResult>> EmitirTokensYCrearSesionAsync(
        AuthenticationContext context, CancellationToken ct)
    {
        // 1. Obtener permisos efectivos
        var permisoResult = await _claimBuilder.BuildPermissionClaimsAsync(context, ct);
        if (permisoResult.IsFailure)
            return Result<AuthenticationResult>.Failure(permisoResult.Error!);

        // 2. Generar tokens
        var genResult = _tokenIssuer.Generate(context, permisoResult.Value);

        // 3. Crear sesión
        var sessionResult = await _sessionManager.CreateSessionAsync(
            context, genResult.Jti, genResult.ExpiresAt, genResult.RefreshHash, ct);
        var sessionId = sessionResult.IsSuccess ? sessionResult.Value : (Guid?)null;

        // 4. Telemetría
        _logger.LogInformation(AuthenticationEvents.TokenGenerated,
            "Login completado | Usuario={IdUsuario} Tenant={IdTenant} App={IdApp} Permisos={Count} Origen={Origen} SessionId={SessionId} Jti={Jti}",
            context.IdUsuario, context.IdTenant, context.IdApp,
            permisoResult.Value.Count, context.Origen, sessionId, genResult.Jti);

        return Result<AuthenticationResult>.Success(new AuthenticationResult(
            genResult.AccessToken, genResult.RefreshToken,
            genResult.Jti, genResult.ExpiresAt, sessionId, context.Origen));
    }
}
