using System.Diagnostics;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Services.Security;

public sealed class PasswordExpirationOptions
{
    public const string SectionName = "PasswordExpiration";
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);
    public int[] WarningDays { get; set; } = [15, 7, 3, 1];
    public bool Enabled { get; set; } = true;
    public int CheckIntervalHours
    {
        get => (int)CheckInterval.TotalHours;
        set => CheckInterval = TimeSpan.FromHours(value);
    }
}

public class PasswordExpirationBackgroundService : BackgroundService, IBackgroundJobStatus
{
    private readonly IEmailQueue _emailQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PasswordExpirationBackgroundService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly PasswordExpirationOptions _options;
    private readonly BackgroundJobState _state = new();

    public string Nombre => nameof(PasswordExpirationBackgroundService);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public PasswordExpirationBackgroundService(
        IEmailQueue emailQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<PasswordExpirationBackgroundService> logger,
        CBP.Logging.Interfaces.ILoggerService olog,
        IOptions<PasswordExpirationOptions> options)
    {
        _emailQueue = emailQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _olog = olog;
        _options = options.Value;
    }

    private void LogSafe(Action logAction)
    {
        try { logAction(); } catch { /* logger disposed during shutdown */ }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_options.Enabled)
            {
                _state.MarcarEjecutando();
                LogSafe(() => _logger.LogInformation("PasswordExpirationBackgroundService iniciado - Intervalo: {Interval}", _options.CheckInterval));
                _olog.LogInformation(new LogEvent
                {
                    EventName = LoggingEvents.BackgroundJobStarted,
                    Scope = LoggingScopes.BackgroundJobs,
                    Message = "PasswordExpirationBackgroundService iniciado",
                    Properties = new Dictionary<string, object?>
                    {
                        [LoggingPropertyNames.Category] = LoggingCategories.Background,
                        [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                        [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                        [LoggingPropertyNames.ElapsedMs] = 0,
                    }
                });
                await RunOnceAsync(stoppingToken);

                using var timer = new PeriodicTimer(_options.CheckInterval);
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunOnceAsync(stoppingToken);
                }
            }
            else
            {
                LogSafe(() => _logger.LogInformation("PasswordExpirationBackgroundService deshabilitado por configuración"));
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _state.MarcarDetenido();
            LogSafe(() => _logger.LogInformation("PasswordExpirationBackgroundService detenido"));
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFinished,
                Scope = LoggingScopes.BackgroundJobs,
                Message = "PasswordExpirationBackgroundService detenido",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _state.MarcarDetenido();
            LogSafe(() => _logger.LogCritical(ex, "Error fatal en PasswordExpirationBackgroundService"));
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFailed,
                Scope = LoggingScopes.BackgroundJobs,
                Message = $"PasswordExpirationBackgroundService falló: {ex.Message}",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        LogSafe(() => _logger.LogInformation("Iniciando verificación de expiración de contraseñas"));
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobStarted,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "Verificación de expiración de contraseñas iniciada",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.ElapsedMs] = 0,
            }
        });

        using var scope = _scopeFactory.CreateScope();
        var historialRepo = scope.ServiceProvider.GetRequiredService<IHistorialPwdRepository>();
        var politicasRepo = scope.ServiceProvider.GetRequiredService<IPoliticaPwdRepository>();
        var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
        var usuarioTenantRepo = scope.ServiceProvider.GetRequiredService<IUsuarioTenantRepository>();
        var auditoriaRepo = scope.ServiceProvider.GetRequiredService<IAuditoriaPwdRepository>();

        try
        {
            var currentDate = DateTime.Now;
            var usuariosConExpiracion = await ObtenerUsuariosConExpiracionAsyncConManejoErrores(historialRepo, politicasRepo, usuarioRepo, usuarioTenantRepo, currentDate, ct);

            foreach (var (usuario, diasRestantes, fechaExpira) in usuariosConExpiracion)
            {
                if (ct.IsCancellationRequested) break;

                await ProcesarUsuarioAsync(usuario, diasRestantes, fechaExpira, _emailQueue, auditoriaRepo, ct);
            }

            sw.Stop();
            _state.AgregarProcesados(usuariosConExpiracion.Count);
            LogSafe(() => _logger.LogInformation("Verificación de expiración completada. Usuarios procesados: {Count}", usuariosConExpiracion.Count));
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFinished,
                Scope = LoggingScopes.BackgroundJobs,
                Message = $"Verificación de expiración completada. Usuarios procesados: {usuariosConExpiracion.Count}",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogSafe(() => _logger.LogError(ex, "Error durante verificación de expiración de contraseñas"));
            _olog.LogError(new LogEvent
            {
                EventName = LoggingEvents.BackgroundJobFailed,
                Scope = LoggingScopes.BackgroundJobs,
                Message = $"Verificación de expiración falló: {ex.Message}",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.Background,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                    [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                    [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                }
            });
        }
    }

    private static async Task<List<(UsuarioDto Usuario, int DiasRestantes, DateTime FechaExpira)>> ObtenerUsuariosConExpiracionAsyncConManejoErrores(
        IHistorialPwdRepository historialRepo,
        IPoliticaPwdRepository politicasRepo,
        IUsuarioRepository usuarioRepo,
        IUsuarioTenantRepository usuarioTenantRepo,
        DateTime currentDate,
        CancellationToken ct)
    {
        try
        {
            var politicasResult = await politicasRepo.ObtenerPorTenantAsync(0, ct);
            if (politicasResult.IsFailure) return [];

            var politicas = politicasResult.Value.Where(p => p.Activa && p.DiasVigencia > 0).ToList();
            if (politicas.Count == 0) return [];

            var usuariosResult = await usuarioRepo.GetAllAsync(true, ct);
            if (usuariosResult.IsFailure) return [];

            var result = new List<(UsuarioDto, int, DateTime)>();

            foreach (var usuario in usuariosResult.Value.Where(u => !u.Eliminado && u.EmailVerificado && !string.IsNullOrWhiteSpace(u.Email)))
            {
                var membresiasResult = await usuarioTenantRepo.ObtenerActivosPorUsuarioAsync(usuario.Id, ct);
                if (membresiasResult.IsFailure || membresiasResult.Value.Count == 0) continue;

                var tenantIds = membresiasResult.Value
                    .Where(m => m.IdEstado == (int)EEstadoUsuario.Activo)
                    .Select(m => m.IdTenant)
                    .Distinct()
                    .ToList();
                if (tenantIds.Count == 0) continue;
                var idTenant = tenantIds.First();

                var politica = politicas.FirstOrDefault(p => p.IdTenant != null && tenantIds.Contains(p.IdTenant.Value))
                    ?? politicas.FirstOrDefault(p => p.IdTenant == null);
                if (politica == null) continue;

                var historialResult = await historialRepo.ObtenerHistorialRecienteAsync(usuario.Id, 1, ct);
                if (historialResult.IsFailure || historialResult.Value.Count == 0) continue;

                var passwordActual = historialResult.Value[0];
                if (passwordActual.FecExpira == null) continue;

                var diasRestantes = (int)(passwordActual.FecExpira.Value - currentDate).TotalDays;
                if (diasRestantes <= 0 || Array.Exists(new[] { 15, 7, 3, 1 }, d => d == diasRestantes))
                {
                    result.Add((new UsuarioDto
                    {
                        Id = usuario.Id,
                        IdTenant = idTenant,
                        Email = usuario.Email,
                        NomUsuario = usuario.NomUsuario,
                        EmailVerificado = usuario.EmailVerificado
                    }, Math.Max(0, diasRestantes), passwordActual.FecExpira.Value));
                }
            }

            return result;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private async Task ProcesarUsuarioAsync(
        UsuarioDto usuario,
        int diasRestantes,
        DateTime fechaExpira,
        IEmailQueue emailQueue,
        IAuditoriaPwdRepository auditoriaRepo,
        CancellationToken ct)
    {
        var templateCode = diasRestantes == 0
            ? "password-expired"
            : $"password-expiration-{diasRestantes}";

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            await emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.PasswordExpired,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["DiasRestantes"] = diasRestantes,
                    ["FechaExpira"] = fechaExpira.ToString("dd/MM/yyyy"),
                    ["TipoEvento"] = diasRestantes == 0 ? "PasswordExpired" : $"PasswordExpirationWarning_{diasRestantes}d",
                    ["TemplateCode"] = templateCode,
                    ["AppName"] = "PassPlat"
                },
                IdTenant: usuario.IdTenant,
                IdUsuario: usuario.Id), ct);
        }

        var auditResult = auditoriaRepo.RegistrarAuditoria(
            usuario.Id,
            (byte)(diasRestantes == 0 ? 8 : 9),
            usuario.IdTenant,
            0,
            usuario.Id,
            null,
            null,
            null,
            null,
            $"Contraseña expira en {diasRestantes} día(s) - {fechaExpira:dd/MM/yyyy}",
            diasRestantes == 0 ? 3 : 2);
        if (auditResult.IsFailure)
            LogSafe(() => _logger.LogWarning("Error registrando auditoría de expiración: {Error}", auditResult.Error!.Message));
    }
}