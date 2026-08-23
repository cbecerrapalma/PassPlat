using AutoMapper;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IBloqueoService : IServiceAsync<Bloqueo, BloqueoDto>
{
    Task<Result<bool>> EstaBloqueadoAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<BloqueoDto?>> ObtenerBloqueoActivoAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<BloqueoDto>> CrearBloqueoAsync(CrearBloqueoDto dto, CancellationToken ct = default);
    Task<Result> DesactivarBloqueosVencidosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default);
}

public class BloqueoService : ServiceAsync<Bloqueo, BloqueoDto>, IBloqueoService
{
    private readonly BloqueoRepository _repo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<BloqueoService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BloqueoService(BloqueoRepository repo, IMapper mapper, IUsuarioRepository usuarioRepo, IEmailQueue emailQueue, ILogger<BloqueoService> logger,
        CBP.Logging.Interfaces.ILoggerService olog, IHttpContextAccessor httpContextAccessor)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _logger = logger;
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<bool>> EstaBloqueadoAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.EstaBloqueadoAsync(idUsuario, idTenant, ct);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        return Result<bool>.Success(result.Value);
    }

    public async Task<Result<BloqueoDto?>> ObtenerBloqueoActivoAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var bloqueoResult = await _repo.ObtenerBloqueoActivoAsync(idUsuario, idTenant, ct);
        if (bloqueoResult.IsFailure) return Result<BloqueoDto?>.Failure(bloqueoResult.Error!);
        var bloqueo = bloqueoResult.Value;
        var dto = bloqueo != null ? Mapper.Map<BloqueoDto>(bloqueo) : null;
        return Result<BloqueoDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<BloqueoDto>> CrearBloqueoAsync(CrearBloqueoDto dto, CancellationToken ct = default)
    {
        var bloqueoResult = _repo.CrearBloqueo(dto.IdUsuario, dto.IdTenant, dto.IdTipoBloqueo, dto.Motivo, dto.FecFin, dto.IdAgente, dto.IdIP, dto.IdUsrBloqueador, dto.TipoDeteccion);
        if (bloqueoResult.IsFailure) return Result<BloqueoDto>.Failure(bloqueoResult.Error!);
        _olog.LogWarning(new LogEvent
        {
            EventName = LoggingEvents.AccountLocked,
            Scope = LoggingScopes.Authorization,
            Message = "Cuenta bloqueada",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                [LoggingPropertyNames.Operation] = LoggingOperations.Create,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = dto.IdUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = dto.IdTenant,
            }
        });
        var result = Result<BloqueoDto>.Success(Mapper.Map<BloqueoDto>(bloqueoResult.Value));
        var notifResult = await NotificarBloqueoAsync(dto.IdUsuario, dto.FecFin, dto.IdTenant, null, ct);
        if (notifResult.IsFailure)
            _logger.LogWarning("Bloqueo creado pero falló la notificación al usuario {IdUsuario}: {Error}", dto.IdUsuario, notifResult.Error?.Message);
        return result;
    }

    public async Task<Result> DesactivarBloqueosVencidosAsync(CancellationToken ct = default)
    {
        var vencidosResult = await _repo.ObtenerBloqueosTemporalesVencidosAsync(ct);
        if (vencidosResult.IsFailure) return Result.Failure(vencidosResult.Error!);
        var vencidos = vencidosResult.Value;

        foreach (var bloqueo in vencidos)
        {
            var notifResult = await NotificarDesbloqueoAsync(bloqueo.IdUsuario, bloqueo.IdTenant, ct);
            if (notifResult.IsFailure)
                _logger.LogWarning("Desbloqueo de usuario {IdUsuario} sin notificación por email: {Error}", bloqueo.IdUsuario, notifResult.Error?.Message);
        }

        var repoResult = await _repo.DesactivarBloqueosVencidosAsync(ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        if (vencidos.Count > 0)
        {
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.AccountUnlocked,
                Scope = LoggingScopes.Authorization,
                Message = "Cuentas desbloqueadas tras vencimiento",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Update,
                    [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                }
            });
        }
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var bloqueosResult = await _repo.ObtenerBloqueosPorUsuarioAsync(idUsuario, idTenant, ct);
        if (bloqueosResult.IsFailure) return Result<IReadOnlyList<BloqueoDto>>.Failure(bloqueosResult.Error!);
        var bloqueos = bloqueosResult.Value;
        return Result<IReadOnlyList<BloqueoDto>>.Success(Mapper.Map<IReadOnlyList<BloqueoDto>>(bloqueos));
    }

    private async Task<Result> NotificarDesbloqueoAsync(int idUsuario, int idTenant, CancellationToken ct)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return Result.Failure(usuarioResult.Error!);
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return Result.Success();

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.UserUnblocked,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["FechaDesbloqueo"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                idTenant,
                usuario.Id,
                null,
                null), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de desbloqueo para usuario {IdUsuario}", idUsuario);
            return Result.Failure("NOTIFY_ERROR", "Error al encolar notificación de desbloqueo");
        }
    }

    private async Task<Result> NotificarBloqueoAsync(int idUsuario, DateTime? fecFin, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return Result.Failure(usuarioResult.Error!);
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return Result.Success();

            var minutes = fecFin.HasValue
                ? Math.Max(1, (int)(fecFin.Value - DateTime.Now).TotalMinutes)
                : 30;

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.AccountLocked,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["Minutes"] = minutes },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de bloqueo para usuario {IdUsuario}", idUsuario);
            return Result.Failure("NOTIFY_ERROR", "Error al encolar notificación de bloqueo");
        }
    }
}
