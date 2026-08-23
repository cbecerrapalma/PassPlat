using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CBP.Data.Abstractions;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Aplicacion.Services.Dashboard;

namespace PassPlat.Aplicacion.Services;

public class IdenExtTokensRotacionJob : BackgroundService, IBackgroundJobStatus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdenExtTokensRotacionJob> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly BackgroundJobState _state = new();
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UmbralRenovacion = TimeSpan.FromDays(7);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    public string Nombre => nameof(IdenExtTokensRotacionJob);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    public IdenExtTokensRotacionJob(IServiceScopeFactory scopeFactory, ILogger<IdenExtTokensRotacionJob> logger, CBP.Logging.Interfaces.ILoggerService olog)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _olog = olog;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("IdenExtTokensRotacionJob iniciado");
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobStarted,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "IdenExtTokensRotacionJob iniciado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.ElapsedMs] = 0,
            }
        });
        _state.MarcarEjecutando();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Intervalo, stoppingToken);
                await ProcesarTokensVencidosAsync(stoppingToken);
                _state.RegistrarCiclo();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _state.MarcarDetenido();
                _olog.LogInformation(new LogEvent
                {
                    EventName = LoggingEvents.BackgroundJobFinished,
                    Scope = LoggingScopes.BackgroundJobs,
                    Message = "IdenExtTokensRotacionJob detenido",
                    Properties = new Dictionary<string, object?>
                    {
                        [LoggingPropertyNames.Category] = LoggingCategories.Background,
                        [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                        [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                        [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
                    }
                });
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IdenExtTokensRotacionJob");
                _olog.LogError(new LogEvent
                {
                    EventName = LoggingEvents.BackgroundJobFailed,
                    Scope = LoggingScopes.BackgroundJobs,
                    Message = $"IdenExtTokensRotacionJob falló: {ex.Message}",
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
        sw.Stop();
        _state.MarcarDetenido();
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.BackgroundJobFinished,
            Scope = LoggingScopes.BackgroundJobs,
            Message = "IdenExtTokensRotacionJob finalizado",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.Background,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.Source] = LoggingSources.SqlServer,
                [LoggingPropertyNames.ElapsedMs] = sw.Elapsed.TotalMilliseconds,
            }
        });
    }

    private async Task ProcesarTokensVencidosAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        var tokensRepo = scope.ServiceProvider.GetRequiredService<IIdenExtTokensRepository>();
        var confRepo = scope.ServiceProvider.GetRequiredService<IConfProvIdenRepository>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<IExternalIdentityProvider>>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkAsync>();

        var tokensResult = await tokensRepo.ObtenerTokensPorRenovarAsync(UmbralRenovacion, ct);
        if (tokensResult.IsFailure || (tokensResult.Value?.Count ?? 0) == 0)
            return;

        var lockResult = await lockService.AcquireLockAsync("IdenExtTokensRotacionJob", LockTimeout, ct);
        if (lockResult.IsFailure)
        {
            _logger.LogWarning("No se pudo adquirir lock distribuido: {Error}", lockResult.Error?.Message);
            return;
        }

        await using var lockHandle = lockResult.Value;

        foreach (var token in tokensResult.Value!)
        {
            try
            {
                var idenExt = token.IdenExt;
                if (idenExt?.ProvIden == null) continue;

                var provider = providers.FirstOrDefault(p => p.ProviderCode == idenExt.ProvIden.Codigo);
                if (provider == null || !provider.SupportsRefreshToken) continue;

                var confResult = await confRepo.ObtenerConfiguracionAsync(idenExt.IdTenant, idenExt.IdProvIden, ct);
                if (confResult.IsFailure || confResult.Value == null) continue;
                var conf = confResult.Value;

                var clientSecret = encryption.Decrypt(conf.ClientSecret, "ConfProvIden");
                if (token.RefreshTokenEnc == null) continue;

                var refreshTokenEncStr = Encoding.UTF8.GetString(token.RefreshTokenEnc);
                var refreshToken = encryption.Decrypt(refreshTokenEncStr, "IdenExtTokens");

                var refreshResult = await provider.RefreshTokenAsync(refreshToken, conf.ClientId, clientSecret, conf.Scopes, ct);
                if (refreshResult.IsFailure)
                {
                    _logger.LogWarning("Fallo renovación token {TokenId} ({Provider}): {Error}",
                        token.Id, provider.ProviderCode, refreshResult.Error?.Message);
                    continue;
                }

                var now = DateTime.Now;
                var correlationId = Guid.NewGuid().ToString("N");
                var rawQuery = uow.RawQuery;

                var spResult = await rawQuery.QuerySPWithOutputAsync<EmptyResult>(
                    "SP_Auth_RenovarTokenProveedor",
                    new RawParameter[]
                    {
                        RawParameter.BigInt("@IdIdenExtTokens", token.Id),
                        RawParameter.BigInt("@IdIdenExt", token.IdIdenExt),
                        RawParameter.In("@AccessTokenEnc", Encoding.UTF8.GetBytes(encryption.Encrypt(refreshResult.Value.AccessToken, "IdenExtTokens")), DbType.Binary, 4000),
                        RawParameter.NVarChar("@AccessTokenHash", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshResult.Value.AccessToken))), 128),
                        RawParameter.Date("@AccessTokenExpires", now.AddSeconds(refreshResult.Value.ExpiresIn)),
                        RawParameter.In("@RefreshTokenEnc", token.RefreshTokenEnc, DbType.Binary, 4000),
                        RawParameter.NVarChar("@RefreshTokenHash", token.RefreshTokenHash, 128),
                        RawParameter.Date("@RefreshTokenExpires", token.RefreshTokenExpires),
                        RawParameter.In("@Scope", token.Scope, DbType.String, 1000),
                        RawParameter.NVarChar("@TokenType", token.TokenType, 50),
                        RawParameter.NVarChar("@CorrelationId", correlationId, 50),
                        RawParameter.In("@RowVersion", token.RowVersion, DbType.Binary, 8),
                        RawParameter.Out("@NuevoId", DbType.Int64, 8)
                    }, ct);

                if (spResult.IsFailure)
                {
                    _logger.LogWarning("Error SP renovación token {TokenId}: {Error}", token.Id, spResult.Error?.Message);
                    continue;
                }

                var nuevoId = spResult.Value.GetOutput<long>("@NuevoId");

                _logger.LogInformation("Token {TokenId} renovado via SP (nuevo Id={NuevoId})", token.Id, nuevoId);
                _state.AgregarProcesados(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando token {TokenId}", token.Id);
            }
        }
    }

    private sealed class EmptyResult { }
}
