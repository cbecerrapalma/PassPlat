using System.Text.Json;
using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
using CBP.Logging;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using CBP.Outbox;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Infrastructure;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Aplicacion.Tests.Tests.Framework.S17;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;
using OutboxProcessor = PassPlat.Aplicacion.Services.Infrastructure.OutboxProcessor;

namespace PassPlat.Aplicacion.Tests.Tests.S81;

/// <summary>
/// S81 — `NewDeviceDetectedEvent` viaja por Outbox: la detección solo agrega la fila al contexto
/// (se guarda con el commit del controlador) y `OutboxProcessor` la publica después.
/// InMemory como `S80EventoDispositivoNuevoTests`.
/// </summary>
public class S81EventoTrasCommitTests
{
    private static PassPlatDbContext CrearContext(string? nombreBd = null) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(nombreBd ?? Guid.NewGuid().ToString()).Options);

    // S136 · D9 — registro de los eventos de PassPlat, como en AplicacionDependencyInjection
    private static OutboxEventRegistry Registro()
    {
        var registro = new OutboxEventRegistry();
        registro.Registrar(typeof(NewIpDetectedEvent));
        registro.Registrar(typeof(NewDeviceDetectedEvent));
        return registro;
    }

    private static DispConfiableService CrearServicio(PassPlatDbContext ctx, Mock<IEventPublisher> publisher,
        IOutboxWriter? outboxWriter = null, ILoggerService? olog = null)
    {
        var auditoria = new Mock<IAuditoriaPwdRepository>();
        auditoria.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Failure("AUD", "sin auditoria"));
        return new DispConfiableService(
            new DispConfiableRepository(ctx), new DispRepository(ctx),
            outboxWriter ?? new OutboxWriter(new OutboxRepository(ctx, Mock.Of<IUnitOfWorkAsync>()), Registro()),
            Mock.Of<IMapper>(), publisher.Object, auditoria.Object,
            Mock.Of<IHttpContextAccessor>(), olog ?? Mock.Of<ILoggerService>(), NullLogger<DispConfiableService>.Instance);
    }

    // S136 · D9 — el procesador lee del IOutboxStore del scope (store simulado: ExecuteUpdate no corre en InMemory)
    private static OutboxProcessor CrearProcesador(Mock<IOutboxStore> store, Mock<IEventPublisher> publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(store.Object);
        services.AddSingleton(publisher.Object);
        return new OutboxProcessor(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Registro(),
            Microsoft.Extensions.Options.Options.Create(new OutboxOptions()), TimeProvider.System,
            NullLogger<CBP.Outbox.OutboxProcessor>.Instance, Mock.Of<ILoggerService>());
    }

    private static Mock<IOutboxStore> StoreCon(OutboxMessage mensaje)
    {
        var store = new Mock<IOutboxStore>();
        store.Setup(s => s.LiberarAtascadosAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(0));
        store.Setup(s => s.PurgarPublicadosAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(0));
        store.Setup(s => s.ObtenerPendientesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<OutboxMessage>>.Success([mensaje]));
        store.Setup(s => s.ReclamarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<bool>.Success(true));
        store.Setup(s => s.MarcarPublicadoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        store.Setup(s => s.ReprogramarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        return store;
    }

    // TipoDisp 1 "PC", Disp 1 (ACME / X1), Usuario 1 en tenant 2; sin filas de DispConfiables
    private static async Task SembrarAsync(PassPlatDbContext ctx)
    {
        var tipo = new TipoDisp { Id = 1, Nombre = "PC" };
        ctx.Add(tipo);
        ctx.Add(new Disp { Id = 1, IdTipoDisp = 1, TipoDisp = tipo, Fabricante = "ACME", Modelo = "X1" });
        ctx.Usuarios.Add(new Usuario { Id = 1, IdTenant = 2, IdEstado = 1, NomUsuario = "u1", Nombre = "U", Apellido = "Uno" });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task DetectarNuevo_SinFila_NoPublicaYPreparaOutbox()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        var publisher = new Mock<IEventPublisher>();
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC nuevo", null, "10.0.0.1", "agente", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, ctx.Outbox.AsNoTracking().Count());
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var fila = Assert.Single(ctx.Outbox.AsNoTracking());
        Assert.Equal("NewDeviceDetectedEvent", fila.EventType);
        Assert.Equal("pending", fila.Status);
        Assert.Equal(2, fila.IdTenant);
        Assert.Equal(1, fila.IdUsuario);
        Assert.False(string.IsNullOrEmpty(fila.CorrelationId));
    }

    [Fact]
    public async Task DetectarNuevo_SinCommit_NoDejaFilaOutbox()
    {
        var nombreBd = Guid.NewGuid().ToString();
        var ctx = CrearContext(nombreBd);
        await SembrarAsync(ctx);
        var servicio = CrearServicio(ctx, new Mock<IEventPublisher>());

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC nuevo", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        using var otro = CrearContext(nombreBd);
        Assert.Equal(0, otro.Outbox.Count());
        Assert.Equal(0, otro.DispConfiables.Count());
    }

    [Fact]
    public async Task DetectarNuevo_OutboxDevuelveFallo_EmiteEventFailedYDevuelveExito()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        var outboxWriter = new Mock<IOutboxWriter>();
        outboxWriter.Setup(o => o.EncolarAsync(It.IsAny<ICBPEvent>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("DB_ERROR", "x"));
        var olog = new CapturingLoggerService();
        var servicio = CrearServicio(ctx, new Mock<IEventPublisher>(), outboxWriter.Object, olog);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC nuevo", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(olog.Events(LoggingEvents.EventFailed));
    }

    [Fact]
    public async Task Procesador_NewDeviceDetected_PublicaEventoConPayload()
    {
        var fecha = new DateTime(2026, 10, 30, 12, 0, 0);
        var evento = new NewDeviceDetectedEvent(1, 2, 1, "PC nuevo", "PC", "ACME", "X1", "10.0.0.1", "agente") { FechaDeteccion = fecha }
            .WithCorrelationId("corr-81");
        var outbox = OutboxMessage.Desde(evento, 2, 1);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var store = StoreCon(outbox);

        var publicados = await CrearProcesador(store, publisher).ProcesarLoteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, publicados);
        Assert.Equal("NewDeviceDetectedEvent", outbox.EventType);
        store.Verify(s => s.MarcarPublicadoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.PublishAsync(
            It.Is<NewDeviceDetectedEvent>(e =>
                e.IdUsuario == 1 && e.IdTenant == 2 && e.IdDisp == 1 &&
                e.NombreDispositivo == "PC nuevo" && e.TipoDispositivo == "PC" &&
                e.Fabricante == "ACME" && e.Modelo == "X1" &&
                e.IpAddress == "10.0.0.1" && e.UserAgent == "agente" &&
                e.FechaDeteccion == fecha && e.CorrelationId == "corr-81"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    // S136 · D9 — un fallo de publicación ya no lanza al llamador: el procesador reprograma la fila
    public async Task Procesador_NewDeviceDetected_PublishFalla_Reprograma()
    {
        var outbox = OutboxMessage.Desde(new NewDeviceDetectedEvent(1, 2, 1, null, null, null, null, null, null).WithCorrelationId("corr-81"), 2, 1);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("PUB", "fallo"));
        var store = StoreCon(outbox);

        var publicados = await CrearProcesador(store, publisher).ProcesarLoteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, publicados);
        store.Verify(s => s.ReprogramarAsync(It.IsAny<long>(), It.IsAny<DateTime>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.MarcarPublicadoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Repositorio_SinObtenerPorUsuarioYDispositivo()
    {
        Assert.Null(typeof(IDispConfiableRepository).GetMethod("ObtenerPorUsuarioYDispositivoAsync"));
        Assert.Null(typeof(DispConfiableRepository).GetMethod("ObtenerPorUsuarioYDispositivoAsync"));
    }
}
