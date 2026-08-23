using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
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
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default);
    Task<Result> MarcarComoConfiableAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, CancellationToken ct = default);
    Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result> DetectarNuevoDispositivoAsync(int idUsuario, int idTenant, int idDisp, string? nombre, int? idAgente, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
    Task<Result> BloquearAsync(int id, CancellationToken ct = default);
}

public class DispConfiableService : ServiceAsync<DispConfiable, DispConfiableDto>, IDispConfiableService
{
    private readonly DispConfiableRepository _repo;
    private readonly IEventPublisher _eventPublisher;
    private readonly IAuditoriaPwdRepository _auditoriaRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILoggerService _olog;
    private readonly ILogger<DispConfiableService> _logger;

    public DispConfiableService(DispConfiableRepository repo, IMapper mapper, IEventPublisher eventPublisher, IAuditoriaPwdRepository auditoriaRepo, IHttpContextAccessor httpContextAccessor, ILoggerService olog, ILogger<DispConfiableService> logger)
        : base(repo, mapper) { _repo = repo; _eventPublisher = eventPublisher; _auditoriaRepo = auditoriaRepo; _httpContextAccessor = httpContextAccessor; _olog = olog; _logger = logger; }

    public async Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default)
    {
        var result = await _repo.EsConfiableAsync(idUsuario, idDisp, ct);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        return Result<bool>.Success(result.Value);
    }

    public async Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var dispositivosResult = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
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

    public async Task<Result> RevocarConfianzaAsync(int idUsuario, int idDisp, CancellationToken ct = default)
    {
        var dispResult = await _repo.ObtenerPorUsuarioYDispositivoAsync(idUsuario, idDisp, ct);
        if (dispResult.IsSuccess && dispResult.Value != null)
        {
            var disp = dispResult.Value;
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

        var repoResult = _repo.RevocarConfianza(idUsuario, idDisp);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DispConfiableDto>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerTodosConDispositivoAsync(ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<DispConfiableDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<DispConfiableDto>>.Success(Mapper.Map<IReadOnlyList<DispConfiableDto>>(repoResult.Value));
    }

    public async Task<Result> EliminarAsync(int id, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorIdAsync(id, ct);
        if (entityResult.IsFailure || entityResult.Value == null)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        var entity = entityResult.Value;
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

    public async Task<Result> BloquearAsync(int id, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorIdAsync(id, ct);
        if (entityResult.IsFailure || entityResult.Value == null)
            return Result.Failure("DISP_NOT_FOUND", "Dispositivo no encontrado");

        var entity = entityResult.Value;
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
        var esConfiable = await EsConfiableAsync(idUsuario, idDisp, ct);
        if (esConfiable.IsSuccess && esConfiable.Value)
        {
            return Result.Success();
        }

        var repoResult = _repo.MarcarComoConfiable(idUsuario, idTenant, idDisp, nombre, idAgente);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);

        var dispResult = await _repo.ObtenerPorUsuarioYDispositivoAsync(idUsuario, idDisp, ct);
        if (dispResult.IsFailure || dispResult.Value == null)
            return Result.Success();

        var dispositivo = dispResult.Value;

        var corrId = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string;
        var evt = new NewDeviceDetectedEvent(
            idUsuario,
            idTenant,
            idDisp,
            dispositivo.Nombre,
            dispositivo.Disp?.TipoDisp?.Nombre,
            dispositivo.Disp?.Fabricante,
            dispositivo.Disp?.Modelo,
            ipAddress,
            userAgent)
        { FechaDeteccion = DateTime.Now };
        if (!string.IsNullOrEmpty(corrId))
            evt = (NewDeviceDetectedEvent)evt.WithCorrelationId(corrId);

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
                Message = "Excepcion al publicar evento NewDeviceDetected",
                Args = [ex.Message],
                Exception = ex,
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Publish,
                    [LoggingPropertyNames.CorrelationId] = corrId,
                    [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                    [LoggingPropertyNames.TenantId] = idTenant,
                }
            });
        }

        return Result.Success();
    }
}
