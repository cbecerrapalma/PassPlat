using AutoMapper;
using CBP.Events;
using CBP.Logging;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Aplicacion.Test.Tests.Framework.S17;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Test.Tests.S32;

/// <summary>
/// DEUDA-009 — Los 5 bloques silenciosos de DispConfiableService ya no lo son:
/// los publishes fallidos emiten Event_Failed (ILoggerService) y las auditorías
/// fallidas emiten una advertencia (ILogger). El flujo principal continúa.
/// </summary>
public class DispConfiableServiceLoggingTests
{
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private static PassPlatDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DispConfiableService CrearServicio(
        PassPlatDbContext ctx,
        CapturingLoggerService olog,
        RecordingLogger<DispConfiableService> logger,
        IEventPublisher eventPublisher,
        IAuditoriaPwdRepository auditoria,
        out DispConfiableRepository repo)
    {
        repo = new DispConfiableRepository(ctx);
        var mapper = new Mock<IMapper>().Object;
        return new DispConfiableService(
            repo, mapper, eventPublisher, auditoria,
            new Mock<IHttpContextAccessor>().Object, olog, logger);
    }

    private static IEventPublisher PublisherQueLanza()
    {
        var mock = new Mock<IEventPublisher>();
        mock.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        return mock.Object;
    }

    private static IAuditoriaPwdRepository AuditoriaFalla()
    {
        var mock = new Mock<IAuditoriaPwdRepository>();
        mock.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Failure("AUD", "sin auditoria"));
        return mock.Object;
    }

    private static async Task<DispConfiable> SembrarDispositivoAsync(PassPlatDbContext ctx, bool confiable)
    {
        var disp = new DispConfiable
        {
            Id = 1,
            IdUsuario = 1,
            IdTenant = 2,
            IdDisp = 1,
            Nombre = "PC de prueba",
            Confiable = confiable,
            FecAlta = DateTime.Now
        };
        ctx.DispConfiables.Add(disp);
        await ctx.SaveChangesAsync();
        return disp;
    }

    [Fact]
    public async Task RevocarConfianza_PublishFallido_EmiteEventFailed_YAuditoriaFallaAdvierte()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: true);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), AuditoriaFalla(), out _);

        var result = await servicio.RevocarConfianzaAsync(1, 1);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(olog.Events(LoggingEvents.EventFailed));
        Assert.Contains(logger.Messages, m => m.Contains("No se pudo registrar auditoria"));
    }

    [Fact]
    public async Task EliminarAsync_AuditoriaFalla_AdvierteSinInterrumpir()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: true);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), AuditoriaFalla(), out _);

        var result = await servicio.EliminarAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Messages, m => m.Contains("No se pudo registrar auditoria de dispositivo eliminado"));
    }

    [Fact]
    public async Task BloquearAsync_AuditoriaFalla_AdvierteSinInterrumpir()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: true);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), AuditoriaFalla(), out _);

        var result = await servicio.BloquearAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Messages, m => m.Contains("No se pudo registrar auditoria de dispositivo bloqueado"));
    }

    [Fact]
    public async Task BloquearAsync_AuditoriaLanzaExcepcion_AdvierteSinInterrumpir()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: true);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();

        var auditoriaMock = new Mock<IAuditoriaPwdRepository>();
        auditoriaMock.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("boom"));
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), auditoriaMock.Object, out _);

        var result = await servicio.BloquearAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Messages, m => m.Contains("Excepcion al registrar auditoria de dispositivo bloqueado"));
    }

    [Fact]
    public async Task DetectarNuevoDispositivo_PublishFallido_EmiteEventFailed()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: false);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), AuditoriaFalla(), out _);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC de prueba", null, null, null);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(olog.Events(LoggingEvents.EventFailed));
    }

    [Fact]
    public async Task DetectarNuevoDispositivo_DispositivoYaConfiable_NoPublicaEvento()
    {
        var ctx = CrearContext();
        await SembrarDispositivoAsync(ctx, confiable: true);
        var olog = new CapturingLoggerService();
        var logger = new RecordingLogger<DispConfiableService>();
        var servicio = CrearServicio(ctx, olog, logger, PublisherQueLanza(), AuditoriaFalla(), out _);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC de prueba", null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Empty(olog.Events(LoggingEvents.EventFailed));
    }
}