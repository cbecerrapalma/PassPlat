using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.Email;

public class EmailBackgroundService : BackgroundService, IBackgroundJobStatus
{
    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly BackgroundJobState _state = new();
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];
    private const int MaxRetries = 3;

    public string Nombre => nameof(EmailBackgroundService);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public EmailBackgroundService(
        IEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundService> logger,
        CBP.Logging.Interfaces.ILoggerService olog)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _olog = olog;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("EmailBackgroundService iniciado");
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobStarted,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "EmailBackgroundService iniciado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.Queue,
                [LoggingPropertyNames.ElapsedMs] = 0,
            }
        });

        var pollTimer = new PeriodicTimer(PollInterval);

        var pollTask = PollPendingEmailsAsync(stoppingToken);
        var queueTask = ProcessQueueAsync(stoppingToken);
        var timerTask = ProcessTimerAsync(pollTimer, stoppingToken);

        _state.MarcarEjecutando();

        try
        {
            await Task.WhenAll(queueTask, timerTask, pollTask);
            sw.Stop();
            _state.MarcarDetenido();
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFinished,
                Scope = LoggingScopes.BackgroundJobs,
                Message = "EmailBackgroundService finalizado",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.Queue,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _state.MarcarDetenido();
            _logger.LogInformation("EmailBackgroundService detenido");
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFinished,
                Scope = LoggingScopes.BackgroundJobs,
                Message = "EmailBackgroundService detenido",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.Queue,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _state.MarcarDetenido();
            _logger.LogError(ex, "Error fatal en EmailBackgroundService");
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFailed,
                Scope = LoggingScopes.BackgroundJobs,
                Message = $"EmailBackgroundService falló: {ex.Message}",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.Queue,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.ReadAllAsync(ct))
        {
            // Se permite que SendFromJobAsync resuelva el email desde IdUsuario
            // (patrón S21: el payload del outbox no transporta ToEmail). Solo se
            // omite si no hay ni ToEmail ni IdUsuario para resolver.
            if (string.IsNullOrWhiteSpace(job.ToEmail) && !job.IdUsuario.HasValue)
            {
                _logger.LogInformation("Omitiendo EmailJob {Kind}: sin email configurado ni usuario para resolver", job.Kind);
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IPassPlatEmailService>();

                var result = await emailService.SendFromJobAsync(job, ct);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Fallo al enviar email {Kind} a {To}: {Error}",
                        job.Kind, job.ToEmail, result.Error?.Message);

                    await HandleRetryAsync(job, result.Error?.Message, ct);
                }
                else
                {
                    _state.AgregarProcesados(1);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al procesar email {Kind} a {To}", job.Kind, job.ToEmail);
                await HandleRetryAsync(job, ex.Message, ct);
            }
        }
    }

    private async Task ProcessTimerAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                // Timer tick serves as additional recovery trigger
                _logger.LogDebug("EmailBackgroundService heartbeat");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PollPendingEmailsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct);
                await ProcessPendingEmailLogsAsync(ct);
                _state.RegistrarCiclo();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en polling de emails pendientes");
            }
        }
    }

    private async Task ProcessPendingEmailLogsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<IEmailLogRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IPassPlatEmailService>();

        var pendingResult = await emailLogRepo.ObtenerPendientesAsync(ct);
        if (pendingResult.IsFailure || pendingResult.Value?.Count == 0)
            return;

        foreach (var log in pendingResult.Value!)
        {
            if (log.Intentos >= MaxRetries || ct.IsCancellationRequested)
                continue;

            _logger.LogInformation("Reintentando email pendiente {LogId} a {Destinatario} (intento {Intento}/{MaxRetries})",
                log.Id, log.Destinatario, log.Intentos + 1, MaxRetries);

            var (extra, kind) = DeserializeExtraFromLog(log);
            var job = new EmailJob(
                Kind: kind,
                ToEmail: log.Destinatario,
                UserName: "",
                Extra: extra,
                IdTenant: log.IdTenant,
                IdUsuario: log.IdUsuario,
                IdApp: log.IdApp,
                CorrelationId: log.CorrelationId,
                EmailLogId: log.Id
            );

            try
            {
                var result = await emailService.SendFromJobAsync(job, ct);
                if (result.IsSuccess)
                {
                    _state.AgregarProcesados(1);
                    _logger.LogInformation("Email {LogId} recuperado exitosamente", log.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reintento {LogId} falló: {Message}", log.Id, ex.Message);
            }
        }
    }

    private static (IReadOnlyDictionary<string, object?> Extra, EmailJobKind Kind) DeserializeExtraFromLog(Dominio.Entities.Core.EmailLog log)
    {
        var extra = new Dictionary<string, object?>();
        var kind = EmailJobKind.PasswordExpired;

        if (!string.IsNullOrWhiteSpace(log.ExtraJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.ExtraJson);
                if (parsed != null)
                {
                    if (parsed.TryGetValue("__EmailJobKind", out var kindEl) &&
                        Enum.TryParse<EmailJobKind>(kindEl.GetString(), out var parsedKind))
                    {
                        kind = parsedKind;
                    }
                    parsed.Remove("__EmailJobKind");

                    foreach (var kv in parsed)
                    {
                        extra[kv.Key] = kv.Value.ValueKind switch
                        {
                            JsonValueKind.String => kv.Value.GetString(),
                            JsonValueKind.Number => kv.Value.GetInt64(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => kv.Value.GetRawText()
                        };
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through with empty Extra + default Kind
            }
        }

        return (extra.AsReadOnly(), kind);
    }

    private async Task HandleRetryAsync(EmailJob failedJob, string? errorMessage, CancellationToken ct)
    {
        var currentAttempt = failedJob.EmailLogId.HasValue ? 1 : 0;

        if (currentAttempt < MaxRetries)
        {
            var delay = RetryDelays[Math.Min(currentAttempt, RetryDelays.Length - 1)];
            _logger.LogInformation("Reintentando email {Kind} a {To} en {Delay} (intento {Attempt}/{MaxRetries})",
                failedJob.Kind, failedJob.ToEmail, delay, currentAttempt + 1, MaxRetries);

            await Task.Delay(delay, ct);

            var retryJob = failedJob with { };
            await _queue.EnqueueAsync(retryJob, ct);
        }
        else
        {
            _logger.LogError("Email {Kind} a {To} agotó {MaxRetries} reintentos. Último error: {Error}",
                failedJob.Kind, failedJob.ToEmail, MaxRetries, errorMessage ?? "desconocido");
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.EmailFailed,
                Scope = LoggingScopes.BackgroundJobs,
                Message = "Email agotó reintentos",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Application,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Send,
                    [LoggingPropertyNames.CorrelationId] = failedJob.CorrelationId,
                    [LoggingPropertyNames.UserId] = failedJob.IdUsuario?.ToString(),
                    [LoggingPropertyNames.TenantId] = failedJob.IdTenant,
                }
            });
        }
    }
}
