using AutoMapper;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Results.Errors;
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
    Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosVisiblesAsync(int idSolicitante, int idUsuario, int idTenant, CancellationToken ct = default);
}

public class BloqueoService : ServiceAsync<Bloqueo, BloqueoDto>, IBloqueoService
{
    private readonly BloqueoRepository _repo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<BloqueoService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;

    public BloqueoService(BloqueoRepository repo, IMapper mapper, IUsuarioRepository usuarioRepo, IEmailQueue emailQueue, ILogger<BloqueoService> logger,
        CBP.Logging.Interfaces.ILoggerService olog, IHttpContextAccessor httpContextAccessor, IUsuarioTenantRepository usuarioTenantRepo)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _logger = logger;
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
        _usuarioTenantRepo = usuarioTenantRepo;
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

    public async Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        var bloqueosResult = await _repo.ObtenerBloqueosPorUsuarioAsync(idUsuario, idTenant, ct);
        if (bloqueosResult.IsFailure) return Result<IReadOnlyList<BloqueoDto>>.Failure(bloqueosResult.Error!);
        var bloqueos = bloqueosResult.Value;
        return Result<IReadOnlyList<BloqueoDto>>.Success(Mapper.Map<IReadOnlyList<BloqueoDto>>(bloqueos));
    }

    // S68.2 · D1 — un usuario ve sus bloqueos; para ver los de otro, ese otro debe ser
    // miembro activo del tenant actual (el permiso BLOQUEOS_VER lo exige la política del controlador).
    public async Task<Result<IReadOnlyList<BloqueoDto>>> ObtenerBloqueosVisiblesAsync(int idSolicitante, int idUsuario, int idTenant, CancellationToken ct = default)
    {
        if (idSolicitante != idUsuario)
        {
            var membresia = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, idTenant, ct);
            if (membresia.IsFailure) return Result<IReadOnlyList<BloqueoDto>>.Failure(membresia.Error!);
            if (membresia.Value is null)
                return Result<IReadOnlyList<BloqueoDto>>.Failure(CommonErrors.Authorization.Forbidden("bloqueos del usuario"));
        }
        return await ObtenerBloqueosPorUsuarioAsync(idUsuario, idTenant, ct);
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
