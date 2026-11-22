using CBP.Logging;
using CBP.Logging.Models;
using CBP.Outbox;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Services.Dashboard;

namespace PassPlat.Aplicacion.Services.Infrastructure;

// S136 · D8 — el procesador es el genérico de CBP.Outbox; aquí solo queda el estado del panel y los logs de PassPlat
public class OutboxProcessor : CBP.Outbox.OutboxProcessor, IBackgroundJobStatus
{
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly BackgroundJobState _state = new();

    public string Nombre => nameof(OutboxProcessor);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        OutboxEventRegistry registro,
        IOptions<OutboxOptions> options,
        TimeProvider tiempo,
        ILogger<CBP.Outbox.OutboxProcessor> logger,
        CBP.Logging.Interfaces.ILoggerService olog)
        : base(scopeFactory, registro, options, tiempo, logger)
    {
        _olog = olog;
    }

    protected override void AlIniciar()
    {
        LogSafe(() => Logger.LogInformation("OutboxProcessor iniciado - Intervalo: {Interval}s, Batch: {Batch}",
            Opciones.PollIntervalSeconds, Opciones.BatchSize));
        EmitBgLog(LoggingEvents.BackgroundJobStarted, "OutboxProcessor iniciado");
        _state.MarcarEjecutando();
    }

    protected override void AlDetener()
    {
        _state.MarcarDetenido();
        LogSafe(() => Logger.LogInformation("OutboxProcessor detenido"));
        EmitBgLog(LoggingEvents.BackgroundJobFinished, "OutboxProcessor detenido");
    }

    protected override void AlCompletarCiclo(int publicados)
    {
        _state.RegistrarCiclo();
        if (publicados <= 0) return;

        _state.AgregarProcesados(publicados);
        EmitBgLog(LoggingEvents.BackgroundJobFinished, $"OutboxProcessor proceso {publicados} mensajes");
    }

    protected override void AlFallarCiclo(Exception ex) =>
        EmitBgLog(LoggingEvents.BackgroundJobFailed, $"OutboxProcessor ciclo fallo: {ex.Message}");

    private static void LogSafe(Action logAction)
    {
        try { logAction(); } catch { }
    }

    private void EmitBgLog(string eventName, string message)
    {
        LogSafe(() => _olog.LogInformation(new LogEvent
        {
            EventName = eventName,
            Scope = LoggingScopes.BackgroundJobs,
            Message = message,
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = "outbox",
                [LoggingPropertyNames.ElapsedMs] = 0d,
            }
        }));
    }
}
