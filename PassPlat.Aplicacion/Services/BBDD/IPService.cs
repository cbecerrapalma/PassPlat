using AutoMapper;
using CBP.Events;
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
    Task<Result<IPDto?>> ObtenerPorDireccionAsync(string direccion, CancellationToken ct = default);
    Task<Result<IPDto>> ObtenerOCrearAsync(string direccion, byte tipoIP = 0, string? pais = null, string? ciudad = null, CancellationToken ct = default);
    Task<Result> MarcarComoSospechosaAsync(int idIP, CancellationToken ct = default);
    Task<Result<IPDto>> DetectarNuevaIPAsync(int idUsuario, int idTenant, string direccionIP, string? pais = null, string? ciudad = null, string? userAgent = null, string? deviceName = null, CancellationToken ct = default);
    Task<Result<NewIpDetectionResult?>> DetectarNuevaIPConOutboxAsync(int idUsuario, int idTenant, string direccionIP, string? userAgent = null, string? deviceName = null, CancellationToken ct = default);
    Task<Result> VerificarCambioIPAsync(int idUsuario, int idTenant, string direccionIP, CancellationToken ct = default);
}

public class NewIpDetectionResult
{
    public IP IpEntity { get; init; } = null!;
    public Outbox? Outbox { get; init; }

    public NewIpDetectionResult(IP ipEntity, Outbox? outbox)
    {
        IpEntity = ipEntity;
        Outbox = outbox;
    }

    public NewIpDetectionResult(IP ipEntity) : this(ipEntity, null) { }
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

    public async Task<Result<IPDto?>> ObtenerPorDireccionAsync(string direccion, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorDireccionAsync(direccion, ct);
        if (entityResult.IsFailure)
            return Result<IPDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<IPDto?>.Success(Mapper.Map<IPDto?>(entity), allowNull: true);
    }

    public async Task<Result<IPDto>> ObtenerOCrearAsync(string direccion, byte tipoIP = 0, string? pais = null, string? ciudad = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(direccion, tipoIP, pais, ciudad);
        if (repoResult.IsFailure) return Result<IPDto>.Failure(repoResult.Error!);
        return Result<IPDto>.Success(Mapper.Map<IPDto>(repoResult.Value.Entidad));
    }

    public async Task<Result> MarcarComoSospechosaAsync(int idIP, CancellationToken ct = default)
    {
        var repoResult = _repo.MarcarComoSospechosa(idIP);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<IPDto>> DetectarNuevaIPAsync(int idUsuario, int idTenant, string direccionIP, string? pais = null, string? ciudad = null, string? userAgent = null, string? deviceName = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(direccionIP, 4, pais, ciudad);
        if (repoResult.IsFailure) return Result<IPDto>.Failure(repoResult.Error!);

        var ipEntity = repoResult.Value.Entidad;
        var dto = Mapper.Map<IPDto>(ipEntity);
        return Result<IPDto>.Success(dto);
    }

    public async Task<Result<NewIpDetectionResult?>> DetectarNuevaIPConOutboxAsync(int idUsuario, int idTenant, string direccionIP, string? userAgent = null, string? deviceName = null, CancellationToken ct = default)
    {
        var repoResult = _repo.ObtenerOCrear(direccionIP, 4, null, null);
        if (repoResult.IsFailure) return Result<NewIpDetectionResult?>.Failure(repoResult.Error!);

        var ipEntity = repoResult.Value.Entidad;
        var esNueva = repoResult.Value.EsNueva;

        var corrId = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string ?? Guid.NewGuid().ToString("N");

        if (!esNueva)
        {
            _olog.LogDebug(new LogEvent
            {
                EventName = LoggingEvents.EventHandled,
                Scope = LoggingScopes.DomainEvents,
                Message = "IP ya existente, no se prepara Outbox para NewIpDetectedEvent",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Get,
                    [LoggingPropertyNames.CorrelationId] = corrId,
                    [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                    [LoggingPropertyNames.TenantId] = idTenant,
                    ["ip"] = direccionIP,
                }
            });
            return Result<NewIpDetectionResult?>.Success(new NewIpDetectionResult(ipEntity, null), allowNull: true);
        }

        var payload = JsonSerializer.Serialize(new NewIpDetectedPayload
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdIP = ipEntity.Id,
            DireccionIP = ipEntity.Direccion,
            Pais = ipEntity.Pais,
            Ciudad = ipEntity.Ciudad,
            UserAgent = userAgent,
            DeviceName = deviceName
        });

        var outbox = Outbox.Crear("NewIpDetectedEvent", payload, corrId, idTenant, idUsuario);

        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.EventQueued,
            Scope = LoggingScopes.DomainEvents,
            Message = "Outbox preparado para NewIpDetectedEvent",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                [LoggingPropertyNames.Operation] = LoggingOperations.Queue,
                [LoggingPropertyNames.CorrelationId] = corrId,
                [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = idTenant,
                ["ip"] = direccionIP,
            }
        });

        return Result<NewIpDetectionResult?>.Success(new NewIpDetectionResult(ipEntity, outbox), allowNull: true);
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
