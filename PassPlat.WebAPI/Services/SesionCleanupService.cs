using System.Diagnostics;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;

namespace PassPlat.WebAPI.Services;

public class SesionCleanupService : BackgroundService, IBackgroundJobStatus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SesionCleanupService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly BackgroundJobState _state = new();
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public string Nombre => nameof(SesionCleanupService);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public SesionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SesionCleanupService> logger, CBP.Logging.Interfaces.ILoggerService olog)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _olog = olog;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = Stopwatch.StartNew();
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobStarted,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "SesionCleanupService iniciado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.ElapsedMs] = 0,
            }
        });
        _state.MarcarEjecutando();
        try
        {
            await CleanupExpiredSessionsAsync(stoppingToken);
            _state.RegistrarCiclo();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Interval, stoppingToken);
                await CleanupExpiredSessionsAsync(stoppingToken);
                _state.RegistrarCiclo();
            }
        }
        catch (OperationCanceledException)
        {
        }
        sw.Stop();
        _state.MarcarDetenido();
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobFinished,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "SesionCleanupService finalizado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
            }
        });
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<SesionRepository>();

            var countResult = await repo.DesactivarExpiradasAsync(ct);
            var count = countResult.IsSuccess ? countResult.Value : 0;

            if (count > 0)
            {
                _state.AgregarProcesados(count);
                _logger.LogInformation("SesionCleanup: {Count} sesiones expiradas desactivadas", count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SesionCleanup: error durante limpieza de sesiones expiradas");
        }
    }
}
