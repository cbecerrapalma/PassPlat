using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.Authentication;

public sealed class SessionManager
{
    private readonly SesionRepository _sesionRepo;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(SesionRepository sesionRepo, ILogger<SessionManager> logger)
    {
        _sesionRepo = sesionRepo;
        _logger = logger;
    }

    public async Task<Result<Guid>> CreateSessionAsync(
        AuthenticationContext context, string jti, DateTime expiresAt, string refreshHash, CancellationToken ct)
    {
        var result = await _sesionRepo.CrearSesionAsync(
            context.IdUsuario, context.IdTenant ?? 0, context.IdApp,
            jti, expiresAt, refreshHash,
            context.IdDispositivo, context.IdIp, null, ct);

        if (result.IsFailure)
            return Result<Guid>.Failure(result.Error!);

        _logger.LogInformation(AuthenticationEvents.SessionCreated,
            "Sesión creada | SessionId={SessionId} | Usuario={IdUsuario} | Tenant={IdTenant}",
            result.Value.IdSesion, context.IdUsuario, context.IdTenant);

        return Result<Guid>.Success(result.Value.IdSesion);
    }

    public async Task<Result> RotateRefreshTokenAsync(
        Guid sessionId, string? oldHashRefresh, string newHashRefresh, DateTime newExpiresAt, CancellationToken ct)
    {
        var rotated = await _sesionRepo.IntentarRotarHashRefreshAsync(
            sessionId, oldHashRefresh, newHashRefresh, newExpiresAt, ct);

        if (rotated.IsFailure)
            return Result.Failure(rotated.Error!);

        if (!rotated.Value)
        {
            _logger.LogWarning("Refresh token reuse detected — revoking session {SessionId}", sessionId);
            await _sesionRepo.RevocarSesionAsync(sessionId, ct);
            return Result.Failure("REFRESH_REUSE", "Refresh token ya fue utilizado — sesión revocada por seguridad");
        }

        _logger.LogInformation(AuthenticationEvents.SessionUpdated,
            "Sesión actualizada | SessionId={SessionId}", sessionId);

        return Result.Success();
    }

    public async Task<Result> RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var result = await _sesionRepo.RevocarSesionAsync(sessionId, ct);
        if (result.IsFailure)
            return Result.Failure(result.Error!);

        _logger.LogInformation(AuthenticationEvents.SessionRevoked,
            "Sesión revocada | SessionId={SessionId}", sessionId);

        return Result.Success();
    }

    public async Task<Result<Datos.SPResults.CrearSesionResult?>> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var entity = await _sesionRepo.GetByIdAsync(sessionId, ct);
        if (entity.IsFailure)
            return Result<Datos.SPResults.CrearSesionResult?>.Failure(entity.Error!);

        return Result<Datos.SPResults.CrearSesionResult?>.Success(null, allowNull: true);
    }

    public async Task<Result<Guid?>> ResolveAndRevokeSessionByJtiAsync(int idUsuario, string jti, CancellationToken ct)
    {
        var sessionResult = await _sesionRepo.ObtenerSesionActivaPorJtiAsync(idUsuario, jti, ct);
        if (sessionResult.IsFailure)
            return Result<Guid?>.Failure(sessionResult.Error!);

        var session = sessionResult.Value;
        if (session == null)
        {
            _logger.LogInformation(AuthenticationEvents.SessionRevoked,
                "SwitchToPlatform: no active session found for Jti={Jti} Usuario={IdUsuario}", jti, idUsuario);
            return Result<Guid?>.Success(null, allowNull: true);
        }

        var sessionId = session.Id;
        var revokeResult = await _sesionRepo.RevocarSesionAsync(sessionId, ct);
        if (revokeResult.IsFailure)
            return Result<Guid?>.Failure(revokeResult.Error!);

        _logger.LogInformation(AuthenticationEvents.SessionRevoked,
            "SwitchToPlatform: sesión revivada | SessionId={SessionId} | Usuario={IdUsuario} | Jti={Jti}",
            sessionId, idUsuario, jti);

        return Result<Guid?>.Success(sessionId, allowNull: true);
    }
}
