using System.Diagnostics;
using System.Text.Json;
using CBP.Events;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services.Infrastructure;

public class OutboxProcessor : BackgroundService, IBackgroundJobStatus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly OutboxOptions _options;
    private readonly BackgroundJobState _state = new();

    public string Nombre => nameof(OutboxProcessor);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        CBP.Logging.Interfaces.ILoggerService olog,
        IOptions<OutboxOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _olog = olog;
        _options = options.Value;
    }

    private void LogSafe(Action logAction)
    {
        try { logAction(); } catch { }
    }

    private void EmitBgLog(string eventName, string message, double elapsedMs)
    {
        _olog.LogInformation(new LogEvent
        {
            EventName = eventName,
            Scope = LoggingScopes.BackgroundJobs,
            Message = message,
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = "outbox",
                [LoggingPropertyNames.ElapsedMs] = elapsedMs,
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogSafe(() => _logger.LogInformation("OutboxProcessor iniciado - Intervalo: {Interval}s, Batch: {Batch}",
            _options.PollIntervalSeconds, _options.BatchSize));
        EmitBgLog(LoggingEvents.BackgroundJobStarted, "OutboxProcessor iniciado", 0);
        _state.MarcarEjecutando();

        await ProcessLoopAsync(stoppingToken);

        _state.MarcarDetenido();
        LogSafe(() => _logger.LogInformation("OutboxProcessor detenido"));
        EmitBgLog(LoggingEvents.BackgroundJobFinished, "OutboxProcessor detenido", 0);
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(ct);
                _state.RegistrarCiclo();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSafe(() => _logger.LogError(ex, "Error en ciclo de OutboxProcessor"));
                EmitBgLog(LoggingEvents.BackgroundJobFailed, $"OutboxProcessor ciclo fallo: {ex.Message}", 0);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<IEmailLogRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var resetResult = await outboxRepo.ResetStaleAsync(ct);
        if (resetResult.IsFailure)
            LogSafe(() => _logger.LogWarning("No se pudieron resetear outbox stale: {Error}", resetResult.Error?.Message));

        var pendingResult = await outboxRepo.ObtenerPendientesAsync(_options.BatchSize, ct);
        if (pendingResult.IsFailure)
        {
            LogSafe(() => _logger.LogError("Error obteniendo outbox pendientes: {Error}", pendingResult.Error?.Message));
            return;
        }

        var pending = pendingResult.Value;
        if (pending.Count == 0) return;

        LogSafe(() => _logger.LogInformation("Procesando {Count} mensajes de Outbox", pending.Count));

        foreach (var item in pending)
        {
            if (ct.IsCancellationRequested) break;

            var processingStartedAt = DateTime.UtcNow;
            var claimResult = await outboxRepo.MarcarProcessingAtomicAsync(item.Id, processingStartedAt, ct);
            if (claimResult.IsFailure || claimResult.Value == 0) continue;

            await PublishAsync(item, outboxRepo, emailLogRepo, eventPublisher, ct);
        }

        sw.Stop();
        EmitBgLog(LoggingEvents.BackgroundJobFinished, $"OutboxProcessor proceso {pending.Count} mensajes", (double)sw.ElapsedMilliseconds);
    }

    private async Task PublishAsync(Outbox outbox, IOutboxRepository outboxRepo, IEmailLogRepository emailLogRepo, IEventPublisher eventPublisher, CancellationToken ct)
    {
        try
        {
            await PublishEventAsync(outbox, emailLogRepo, eventPublisher, ct);

            var publishedResult = await outboxRepo.MarcarPublishedAsync(outbox.Id, DateTime.UtcNow, ct);
            if (publishedResult.IsFailure)
                LogSafe(() => _logger.LogWarning("No se pudo marcar published outbox id={Id}: {Error}", outbox.Id, publishedResult.Error?.Message));
            else
                _state.AgregarProcesados(1);
        }
        catch (Exception ex)
        {
            var attempts = outbox.Attempts + 1;

            if (attempts >= _options.MaxRetries)
            {
                var failedResult = await outboxRepo.MarcarFailedAsync(outbox.Id, ex.Message, DateTime.UtcNow, attempts, ct);
                if (failedResult.IsFailure)
                    LogSafe(() => _logger.LogWarning("No se pudo marcar failed outbox id={Id}: {Error}", outbox.Id, failedResult.Error?.Message));

                LogSafe(() => _logger.LogError(ex, "Outbox id={Id} agoto reintentos ({Attempts}). Event: {EventType}", outbox.Id, attempts, outbox.EventType));
            }
            else
            {
                var delayIndex = Math.Min(attempts - 1, _options.RetryDelayMinutes.Length - 1);
                var delay = TimeSpan.FromMinutes(_options.RetryDelayMinutes[delayIndex]);
                var nextAttempt = DateTime.UtcNow.Add(delay);

                var reprogramadoResult = await outboxRepo.ReprogramarAsync(outbox.Id, nextAttempt, attempts, ct);
                if (reprogramadoResult.IsFailure)
                    LogSafe(() => _logger.LogWarning("No se pudo reprogramar outbox id={Id}: {Error}", outbox.Id, reprogramadoResult.Error?.Message));

                LogSafe(() => _logger.LogWarning("Outbox id={Id} reprogramado para {NextAttempt} (intento {Attempts}): {Error}", outbox.Id, nextAttempt, attempts, ex.Message));
            }
        }
    }

    private async Task PublishEventAsync(Outbox outbox, IEmailLogRepository emailLogRepo, IEventPublisher eventPublisher, CancellationToken ct)
    {
        if (outbox.EventType == "NewIpDetectedEvent")
        {
            var payload = JsonSerializer.Deserialize<NewIpDetectedPayload>(outbox.Payload);
            if (payload != null)
            {
                var dedupResult = await emailLogRepo.ExisteNotificacionNuevaIpAsync(payload.IdUsuario, payload.DireccionIP, ct);
                if (dedupResult.IsFailure)
                {
                    LogSafe(() => _logger.LogWarning("No se pudo verificar dedup de NewIp: {Error}", dedupResult.Error?.Message));
                    throw new InvalidOperationException("Fallo la verificacion de idempotencia NewIp");
                }

                if (dedupResult.Value)
                {
                    LogSafe(() => _logger.LogInformation("NewIp dedup: notificacion ya existe para usuario {IdUsuario} IP {Ip} - omitiendo publicacion",
                        payload.IdUsuario, payload.DireccionIP));
                    return;
                }

                var evt = new NewIpDetectedEvent(
                    payload.IdUsuario,
                    payload.IdTenant,
                    payload.IdIP,
                    payload.DireccionIP,
                    payload.Pais,
                    payload.Ciudad,
                    payload.UserAgent,
                    payload.DeviceName);

                if (!string.IsNullOrEmpty(outbox.CorrelationId))
                    evt = (NewIpDetectedEvent)evt.WithCorrelationId(outbox.CorrelationId);

                var publishResult = await eventPublisher.PublishAsync(evt, ct);
                if (publishResult.IsFailure)
                    throw new InvalidOperationException($"PublishAsync fallo para outbox id={outbox.Id}: {publishResult.Error?.Message}");
                return;
            }
        }

        LogSafe(() => _logger.LogWarning("Outbox id={Id} - EventType {EventType} no reconocido, marcando como published", outbox.Id, outbox.EventType));
    }
}
