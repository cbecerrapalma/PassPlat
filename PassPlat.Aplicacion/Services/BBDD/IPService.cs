using AutoMapper;
using CBP.Events;
using CBP.Outbox;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Logging.Models;
using Microsoft.AspNetCore.Http;
using PassPlat.Aplicacion.Dtos.Contexto;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;
using System.Text.Json;

namespace PassPlat.Aplicacion.Services;

public interface IIPService : IServiceAsync<IP, IPDto>
{
    Task<Result<IPDto>> DetectarNuevaIPAsync(int idUsuario, int idTenant, string direccionIP, string? pais = null, string? ciudad = null, string? userAgent = null, string? deviceName = null, CancellationToken ct = default);
    Task<Result> VerificarCambioIPAsync(int idUsuario, int idTenant, string direccionIP, CancellationToken ct = default);
}

public class IPService : ServiceAsync<IP, IPDto>, IIPService
{
    private readonly IPRepository _repo;
    private readonly ISesionRepository _sesionRepo;
    private readonly IEventPublisher _eventPublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILoggerService _olog;

    public IPService(IPRepository repo, ISesionRepository sesionRepo, IMapper mapper, IEventPublisher eventPublisher, IHttpContextAccessor httpContextAccessor, ILoggerService olog)
        : base(repo, mapper) { _repo = repo; _sesionRepo = sesionRepo; _eventPublisher = eventPublisher; _httpContextAccessor = httpContextAccessor; _olog = olog; }

    public async Task<Result<IPDto>> DetectarNuevaIPAsync(int idUsuario, int idTenant, string direccionIP, string? pais = null, string? ciudad = null, string? userAgent = null, string? deviceName = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(direccionIP, 4, pais, ciudad);
        if (repoResult.IsFailure) return Result<IPDto>.Failure(repoResult.Error!);

        var ipEntity = repoResult.Value.Entidad;
        var dto = Mapper.Map<IPDto>(ipEntity);
        return Result<IPDto>.Success(dto);
    }

    public async Task<Result> VerificarCambioIPAsync(int idUsuario, int idTenant, string direccionIP, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorDireccionAsync(direccionIP, ct);
        if (entityResult.IsFailure || entityResult.Value == null)
            return Result.Failure("IP_NOT_FOUND", "IP no encontrada");

        var ip = entityResult.Value;

        var sesionesResult = await _sesionRepo.WhereAsync(s => s.IdUsuario == idUsuario && s.IdIP == ip.Id, false, ct);
        if (sesionesResult.IsFailure || sesionesResult.Value.Count == 0)
        {
            var corrId = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string;
            var evt = new SecurityAlertEvent(
                idUsuario,
                idTenant,
                ip.Id,
                ip.Direccion,
                "NewIpForUser",
                $"Primera vez que el usuario accede desde la IP {direccionIP}")
            { FechaAlerta = DateTime.Now };
            if (!string.IsNullOrEmpty(corrId))
                evt = (SecurityAlertEvent)evt.WithCorrelationId(corrId);

            try
            {
                var publishResult = await _eventPublisher.PublishAsync(evt, ct);
                if (publishResult.IsFailure)
                    _olog.LogError(new LogEvent
                    {
                        EventName = LoggingEvents.EventFailed,
                        Scope = LoggingScopes.DomainEvents,
                        Message = "Fallo al publicar SecurityAlert",
                        Args = [publishResult.Error?.Message ?? "sin detalle"],
                        Properties = new Dictionary<string, object?>
                        {
                            [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                            [LoggingPropertyNames.Operation] = LoggingOperations.Publish,
                            [LoggingPropertyNames.CorrelationId] = corrId,
                            [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                            [LoggingPropertyNames.TenantId] = idTenant,
                            ["ip"] = direccionIP,
                        }
                    });
            }
            catch (Exception ex)
            {
                _olog.LogError(new LogEvent
                {
                    EventName = LoggingEvents.EventFailed,
                    Scope = LoggingScopes.DomainEvents,
                    Message = "Excepcion al publicar evento SecurityAlert",
                    Args = [ex.Message],
                    Exception = ex,
                    Properties = new Dictionary<string, object?>
                    {
                        [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                        [LoggingPropertyNames.Operation] = LoggingOperations.Publish,
                        [LoggingPropertyNames.CorrelationId] = corrId,
                        [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                        [LoggingPropertyNames.TenantId] = idTenant,
                        ["ip"] = direccionIP,
                    }
                });
            }
        }

        return Result.Success();
    }
}
