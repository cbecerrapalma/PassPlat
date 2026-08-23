using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.Infrastructure;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Aplicacion.Test.Tests.Framework.S17;

namespace PassPlat.Aplicacion.Test.Tests.S32;

/// <summary>
/// G11 — Identity DI: IHostedService e IBackgroundJobStatus deben resolver la MISMA
/// instancia concreta (ReferenceEquals true) para cada job y para EmailQueue.
/// Replica el triple registro de AplicacionDependencyInjection.cs y Program.cs.
/// </summary>
public class BackgroundJobDiIdentityTests
{
    private static ServiceProvider CrearProvider()
    {
        var services = new ServiceCollection();
        var olog = new CapturingLoggerService();
        services.AddLogging();
        services.AddSingleton<ILoggerService>(olog);
        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);
        services.AddSingleton<IServiceScopeFactory>(new Mock<IServiceScopeFactory>().Object);

        services.AddSingleton(sp => Microsoft.Extensions.Options.Options.Create(new OutboxOptions()));
        services.AddSingleton(sp => Microsoft.Extensions.Options.Options.Create(new PasswordExpirationOptions { Enabled = true }));

        // EmailQueue: concreto + IEmailQueue + IBackgroundJobStatus (misma instancia)
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<EmailQueue>());

        // EmailBackgroundService: concreto + IHostedService + IBackgroundJobStatus (misma instancia)
        services.AddSingleton<EmailBackgroundService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<EmailBackgroundService>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<EmailBackgroundService>());

        // IdenExtTokensRotacionJob: concreto + IHostedService + IBackgroundJobStatus (misma instancia)
        services.AddSingleton<IdenExtTokensRotacionJob>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<IdenExtTokensRotacionJob>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<IdenExtTokensRotacionJob>());

        // OutboxProcessor: concreto + IHostedService + IBackgroundJobStatus (misma instancia)
        services.AddSingleton<OutboxProcessor>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<OutboxProcessor>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<OutboxProcessor>());

        // PasswordExpirationBackgroundService: concreto + IHostedService + IBackgroundJobStatus (misma instancia)
        // Este job se registra en Program.cs (WebAPI) con el MISMO patrón triple.
        services.AddSingleton<PasswordExpirationBackgroundService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PasswordExpirationBackgroundService>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<PasswordExpirationBackgroundService>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EmailQueue_ResuelveMismaInstancia_ParaIEmailQueueEIBackgroundJobStatus()
    {
        var provider = CrearProvider();

        var concreta = provider.GetRequiredService<EmailQueue>();
        var comoEnqueue = provider.GetRequiredService<IEmailQueue>();
        var comoEstado = provider.GetServices<IBackgroundJobStatus>().OfType<EmailQueue>().Single();

        Assert.Same(concreta, comoEnqueue);
        Assert.Same(concreta, comoEstado);
        Assert.Equal("EmailQueue", comoEstado.Nombre);
        var status = (await comoEstado.ObtenerEstadoAsync()).Value;
        Assert.True(status.Ejecutando);
        Assert.Null(status.UltimaEjecucion);
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("EmailBackgroundService")]
    [InlineData("IdenExtTokensRotacionJob")]
    [InlineData("OutboxProcessor")]
    [InlineData("PasswordExpirationBackgroundService")]
    public async Task CadaJob_ResuelveMismaInstancia_ParaIHostedServiceEIBackgroundJobStatus(string jobName)
    {
        var provider = CrearProvider();

        var concreta = provider.GetServices<IHostedService>()
            .Single(s => s.GetType().Name == jobName);
        var comoEstado = provider.GetServices<IBackgroundJobStatus>()
            .Single(s => s.Nombre == jobName);

        Assert.Same(concreta, comoEstado);

        var statusResult = await comoEstado.ObtenerEstadoAsync();
        Assert.True(statusResult.IsSuccess);
        Assert.False(statusResult.Value.Ejecutando);
        Assert.Equal(0, statusResult.Value.ItemsProcesados);
    }

    [Fact]
    public void SesionCleanupService_RegistroProgramCs_EsCompatibleConPatronTriple()
    {
        // Review-local para el job de WebAPI (no referenciable desde este proyecto):
        // Program.cs registra SesionCleanupService con EXACTAMENTE:
        //   AddSingleton<SesionCleanupService>();
        //   AddSingleton<IHostedService>(sp => sp.GetRequiredService<SesionCleanupService>());
        //   AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<SesionCleanupService>());
        // Este test valida que el patrón triple replica la identidad para un job que
        // implementa BackgroundService + IBackgroundJobStatus, como hace la clase real.
        var services = new ServiceCollection();
        var olog = new CapturingLoggerService();
        services.AddLogging();
        services.AddSingleton<ILoggerService>(olog);
        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);
        services.AddSingleton<IServiceScopeFactory>(new Mock<IServiceScopeFactory>().Object);

        services.AddSingleton<DummyWebApiJob>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DummyWebApiJob>());
        services.AddSingleton<IBackgroundJobStatus>(sp => sp.GetRequiredService<DummyWebApiJob>());
        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<DummyWebApiJob>(),
            provider.GetServices<IHostedService>().OfType<DummyWebApiJob>().Single());
        Assert.Same(
            provider.GetRequiredService<DummyWebApiJob>(),
            provider.GetServices<IBackgroundJobStatus>().OfType<DummyWebApiJob>().Single());
    }
}

/// <summary>
/// Stand-in para SesionCleanupService: implementa el mismo contrato
/// (BackgroundService + IBackgroundJobStatus) para validar el patrón triple de DI.
/// </summary>
public sealed class DummyWebApiJob : BackgroundService, IBackgroundJobStatus
{
    private readonly BackgroundJobState _state = new();

    public string Nombre => nameof(DummyWebApiJob);

    public Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<BackgroundJobStatus>.Success(_state.Snapshot()));

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.CompletedTask;
}