using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
using CBP.Outbox;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IDispConfiableService : IServiceAsync<DispConfiable, DispConfiableDto>
{
    Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(int? idTenant, CancellationToken ct = default);
    Task<Result> MarcarComoConfiableAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, CancellationToken ct = default);
    Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default);
    Task<Result> DetectarNuevoDispositivoAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, int? idTenant, CancellationToken ct = default);
    Task<Result> BloquearAsync(int id, CancellationToken ct = default);
    Task<Result> BloquearAsync(int id, int? idTenant, CancellationToken ct = default);
}

public class DispConfiableService : ServiceAsync<DispConfiable, DispConfiableDto>, IDispConfiableService
{
    private readonly DispConfiableRepository _repo;
    private readonly IDispRepository _dispRepo;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IEventPublisher _eventPublisher;
    private readonly IAuditoriaPwdRepository _auditoriaRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILoggerService _olog;
    private readonly ILogger<DispConfiableService> _logger;

    // S80 · D2 — IDispRepository inyectado tras repo (DI lo resuelve)
    // S81 · D2 / S136 · D8 — IOutboxWriter (CBP.Outbox) tercer parámetro (DI lo resuelve)
    public DispConfiableService(DispConfiableRepository repo, IDispRepository dispRepo, IOutboxWriter outboxWriter, IMapper mapper, IEventPublisher eventPublisher, IAuditoriaPwdRepository auditoriaRepo, IHttpContextAccessor httpContextAccessor, ILoggerService olog, ILogger<DispConfiableService> logger)
        : base(repo, mapper) { _repo = repo; _dispRepo = dispRepo; _outboxWriter = outboxWriter; _eventPublisher = eventPublisher; _auditoriaRepo = auditoriaRepo; _httpContextAccessor = httpContextAccessor; _olog = olog; _logger = logger; }

    public Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default)
        => EsConfiableAsync(idUsuario, idDisp, null, ct);

    // S79 · D3 — idTenant null = modo Plataforma (sin filtro de tenant)
    public async Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default)
    {
        var result = await _repo.EsConfiableAsync(idUsuario, idDisp, idTenant, ct);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        return Result<bool>.Success(result.Value);
    }

    public Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
        => ObtenerPorUsuarioAsync(idUsuario, null, ct);

    public async Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? idTenant, CancellationToken ct = default)
    {
        var dispositivosResult = await _repo.ObtenerPorUsuarioAsync(idUsuario, idTenant, ct);
        if (dispositivosResult.IsFailure) return Result<IReadOnlyList<DispConfiableDto>>.Failure(dispositivosResult.Error!);
        var dispositivos = dispositivosResult.Value;
        return Result<IReadOnlyList<DispConfiableDto>>.Success(Mapper.Map<IReadOnlyList<DispConfiableDto>>(dispositivos));
    }

    public async Task<Result> MarcarComoConfiableAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, CancellationToken ct = default)
    {
        var repoResult = _repo.MarcarComoConfiable(idUsuario, idTenant, idDisp, nombre, idAgente);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, CancellationToken ct = default)
        => RevocarConfianzaAsync(idUsuario, idDisp, null, ct);

    // S79 · D3 — evento y auditoría por cada fila confiable del par en el tenant; null = todos los tenants
    public async Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default)
    {
        var dispsResult = await _repo.WhereAsync(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.Confiable
            && (idTenant == null || d.IdTenant == idTenant), ct: ct);
        if (dispsResult.IsSuccess)
        {
            foreach (var disp in dispsResult.Value)
                await PublicarYAuditarRevocacionAsync(idUsuario, idDisp, disp, ct);
        }

        var repoResult = _repo.RevocarConfianza(idUsuario, idDisp, idTenant);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    private async Task PublicarYAuditarRevocacionAsync(int idUsuario, int idDisp, DispConfiable disp, CancellationToken ct)
    {
        var corrId = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string;
        var evt = new DeviceRevokedEvent(
            idUsuario,
            disp.IdTenant,
            idDisp,
            disp.Nombre,
            "Usuario")
        { FechaRevoca = DateTime.Now };
        if (!string.IsNullOrEmpty(corrId))
            evt = (DeviceRevokedEvent)evt.WithCorrelationId(corrId);

        try
        {
            await _eventPublisher.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.EventFailed,
                Scope = LoggingScopes.DomainEvents,
                Message = "Excepcion al publicar evento DeviceRevoked",
                Args = [ex.Message],
                Exception = ex,
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Publish,
                    [LoggingPropertyNames.CorrelationId] = corrId,
                    [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                    [LoggingPropertyNames.TenantId] = disp.IdTenant,
                }
            });
        }
        var auditResult = _auditoriaRepo.RegistrarAuditoria(
            idUsuario, (byte)4, disp.IdTenant, 0, idUsuario,
            null, null, null, null,
            $"Dispositivo revocado: {disp.Nombre ?? disp.Id.ToString()}", 2);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning("No se pudo registrar auditoria de dispositivo revocado: {Error}", auditResult.Error?.Message);
        }
    }

    public Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default)
        => ObtenerTodosConDispositivoAsync(null, ct);

    public async Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(int? idTenant, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerTodosConDispositivoAsync(idTenant, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<DispConfiableDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<DispConfiableDto>>.Success(Mapper.Map<IReadOnlyList<DispConfiableDto>>(repoResult.Value));
    }

    public Task<Result> EliminarAsync(int id, CancellationToken ct = default)
        => EliminarAsync(id, null, ct);

    // S75 · D2 — un registro de otro tenant se trata igual que uno inexistente
    public async Task<Result> EliminarAsync(int id, int? idTenant, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorIdAsync(id, ct);
        if (entityResult.IsFailure || entityResult.Value == null)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        var entity = entityResult.Value;
        if (idTenant is not null && entity.IdTenant != idTenant)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        try
        {
            var auditResult = _auditoriaRepo.RegistrarAuditoria(
                entity.IdUsuario, 4, entity.IdTenant, 0, entity.IdUsuario,
                null, null, null, null,
                $"Dispositivo eliminado: {entity.Nombre ?? entity.Id.ToString()}", 2);
            if (auditResult.IsFailure)
                _logger.LogWarning("No se pudo registrar auditoria de dispositivo eliminado: {Error}", auditResult.Error?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Excepcion al registrar auditoria de dispositivo eliminado");
        }

        _repo.Remove(entity);
        return Result.Success();
    }

    public Task<Result> BloquearAsync(int id, CancellationToken ct = default)
        => BloquearAsync(id, null, ct);

    // S75 · D2 — un registro de otro tenant se trata igual que uno inexistente
    public async Task<Result> BloquearAsync(int id, int? idTenant, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorIdAsync(id, ct);
        if (entityResult.IsFailure || entityResult.Value == null)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        var entity = entityResult.Value;
        if (idTenant is not null && entity.IdTenant != idTenant)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        entity.Confiable = false;

        try
        {
            var auditResult = _auditoriaRepo.RegistrarAuditoria(
                entity.IdUsuario, 4, entity.IdTenant, 0, entity.IdUsuario,
                null, null, null, null,
                $"Dispositivo bloqueado: {entity.Nombre ?? entity.Id.ToString()}", 2);
            if (auditResult.IsFailure)
                _logger.LogWarning("No se pudo registrar auditoria de dispositivo bloqueado: {Error}", auditResult.Error?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Excepcion al registrar auditoria de dispositivo bloqueado");
        }

        return Result.Success();
    }

    public async Task<Result> DetectarNuevoDispositivoAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var esConfiable = await EsConfiableAsync(idUsuario, idDisp, idTenant, ct);
        if (esConfiable.IsSuccess && esConfiable.Value)
        {
            return Result.Success();
        }

        var repoResult = _repo.MarcarComoConfiable(idUsuario, idTenant, idDisp, nombre, idAgente);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);

        // S80 · D2 — usa la entidad que el repositorio ya tiene en el tracker; no relee de la BD
        var dispositivo = repoResult.Value;

        // S80 · D3 — datos del Disp con su tipo; si falla o falta, el evento sale igual con esos campos nulos
        var dispDetalle = await _dispRepo.ObtenerConDetallesAsync(idDisp, ct);
        var dispConDetalles = dispDetalle.IsSuccess ? dispDetalle.Value : null;

        // S81 · D2 — el evento viaja por Outbox: la fila se guarda con el commit del controlador
        var corrId = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string ?? Guid.NewGuid().ToString("N");
        // S136 · D8 — se encola el evento por IOutboxWriter (CBP.Outbox); EventType de la fila = "NewDeviceDetectedEvent"
        var evento = (NewDeviceDetectedEvent)new NewDeviceDetectedEvent(
            idUsuario,
            idTenant,
            idDisp,
            dispositivo.Nombre,
            dispConDetalles?.TipoDisp?.Nombre,
            dispConDetalles?.Fabricante,
            dispConDetalles?.Modelo,
            ipAddress,
            userAgent)
        { FechaDeteccion = DateTime.Now }.WithCorrelationId(corrId);

        // S81 · D2 — un fallo al preparar el Outbox se registra y la detección sigue sin romper el login
        var fallo = false;
        string? error = null;
        Exception? excepcion = null;
        try
        {
            var addResult = await _outboxWriter.EncolarAsync(evento, idTenant, idUsuario, ct);
            if (addResult.IsFailure) { fallo = true; error = addResult.Error?.Message; }
        }
        catch (Exception ex)
        {
            fallo = true;
            excepcion = ex;
            error = ex.Message;
        }

        if (fallo)
        {
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.EventFailed,
                Scope = LoggingScopes.DomainEvents,
                Message = "No se pudo preparar Outbox para NewDeviceDetectedEvent",
                Args = [error ?? "sin detalle"],
                Exception = excepcion,
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Queue,
                    [LoggingPropertyNames.CorrelationId] = corrId,
                    [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                    [LoggingPropertyNames.TenantId] = idTenant,
                }
            });
            return Result.Success();
        }

        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.EventQueued,
            Scope = LoggingScopes.DomainEvents,
            Message = "Outbox preparado para NewDeviceDetectedEvent",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                [LoggingPropertyNames.Operation] = LoggingOperations.Queue,
                [LoggingPropertyNames.CorrelationId] = corrId,
                [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = idTenant,
            }
        });

        return Result.Success();
    }
}
