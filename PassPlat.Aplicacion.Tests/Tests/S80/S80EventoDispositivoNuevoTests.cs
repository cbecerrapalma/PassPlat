using System.Text.Json;
using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
using CBP.Logging.Interfaces;
using CBP.Outbox;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S80;

/// <summary>
/// S80 — `DetectarNuevoDispositivoAsync` publica `NewDeviceDetectedEvent` con la entidad recién
/// marcada (sin releerla de la BD) y lleva los datos del `Disp` (tipo, fabricante, modelo).
/// InMemory como `S79ConfianzaTenantTests`; `Mock<IEventPublisher>` con `Verify`.
/// S81: el evento viaja por Outbox.
/// </summary>
public class S80EventoDispositivoNuevoTests
{
    private static PassPlatDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DispConfiableService CrearServicio(PassPlatDbContext ctx, Mock<IEventPublisher> publisher)
    {
        var auditoria = new Mock<IAuditoriaPwdRepository>();
        auditoria.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Failure("AUD", "sin auditoria"));
        // S136 · D9 — el evento se encola por el IOutboxWriter de CBP.Outbox sobre el OutboxRepository real
        var registro = new OutboxEventRegistry();
        registro.Registrar(typeof(NewDeviceDetectedEvent));
        return new DispConfiableService(
            new DispConfiableRepository(ctx), new DispRepository(ctx), new OutboxWriter(new OutboxRepository(ctx, Mock.Of<IUnitOfWorkAsync>()), registro),
            Mock.Of<IMapper>(), publisher.Object, auditoria.Object,
            Mock.Of<IHttpContextAccessor>(), Mock.Of<ILoggerService>(), NullLogger<DispConfiableService>.Instance);
    }

    // S81 · D7 — tras el commit, la única fila Outbox y su payload
    private static async Task<JsonElement> PayloadTrasCommitAsync(PassPlatDbContext ctx)
    {
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var fila = Assert.Single(ctx.Outbox.AsNoTracking());
        Assert.Equal("NewDeviceDetectedEvent", fila.EventType);
        return JsonDocument.Parse(fila.Payload).RootElement.Clone();
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
    public async Task DetectarNuevo_SinFila_PublicaEventoConDatosDelDisp()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC nuevo", null, "10.0.0.1", "agente", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        var e = await PayloadTrasCommitAsync(ctx);
        Assert.Equal(1, e.GetProperty("IdUsuario").GetInt32());
        Assert.Equal(2, e.GetProperty("IdTenant").GetInt32());
        Assert.Equal(1, e.GetProperty("IdDisp").GetInt32());
        Assert.Equal("PC nuevo", e.GetProperty("NombreDispositivo").GetString());
        Assert.Equal("PC", e.GetProperty("TipoDispositivo").GetString());
        Assert.Equal("ACME", e.GetProperty("Fabricante").GetString());
        Assert.Equal("X1", e.GetProperty("Modelo").GetString());
        Assert.Equal("10.0.0.1", e.GetProperty("IpAddress").GetString());
        Assert.Equal("agente", e.GetProperty("UserAgent").GetString());
    }

    [Fact]
    public async Task DetectarNuevo_SinFila_NoGuardaYElCommitPersisteLaFila()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "PC nuevo", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ctx.DispConfiables.AsNoTracking().Count());
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var fila = Assert.Single(ctx.DispConfiables.AsNoTracking());
        Assert.Equal(2, fila.IdTenant);
        Assert.True(fila.Confiable);
    }

    [Fact]
    public async Task DetectarNuevo_FilaRevocada_PublicaEventoUnaVezConSuNombre()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        ctx.DispConfiables.Add(new DispConfiable
        {
            Id = 1, IdUsuario = 1, IdTenant = 2, IdDisp = 1, Nombre = "PC viejo",
            Confiable = false, FecAlta = DateTime.Now, UltUso = DateTime.Now
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, null, null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        var e = await PayloadTrasCommitAsync(ctx);
        Assert.Equal("PC viejo", e.GetProperty("NombreDispositivo").GetString());
        var fila = ctx.DispConfiables.AsNoTracking().Single(d => d.Id == 1);
        Assert.True(fila.Confiable);
    }

    [Fact]
    public async Task DetectarNuevo_YaConfiable_NoPublica()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        ctx.DispConfiables.Add(new DispConfiable
        {
            Id = 1, IdUsuario = 1, IdTenant = 2, IdDisp = 1, Nombre = "PC t2",
            Confiable = true, FecAlta = DateTime.Now, UltUso = DateTime.Now
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        var publisher = new Mock<IEventPublisher>();
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 1, "cualquier cosa", null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Empty(ctx.Outbox);
    }

    [Fact]
    public async Task DetectarNuevo_DispInexistente_PublicaConDetallesNulos()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx);
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var servicio = CrearServicio(ctx, publisher);

        var result = await servicio.DetectarNuevoDispositivoAsync(1, 2, 99, null, null, null, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        publisher.Verify(p => p.PublishAsync(It.IsAny<ICBPEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        var e = await PayloadTrasCommitAsync(ctx);
        Assert.Equal(1, e.GetProperty("IdUsuario").GetInt32());
        Assert.Equal(2, e.GetProperty("IdTenant").GetInt32());
        Assert.Equal(99, e.GetProperty("IdDisp").GetInt32());
        Assert.Equal(JsonValueKind.Null, e.GetProperty("NombreDispositivo").ValueKind);
        Assert.Equal(JsonValueKind.Null, e.GetProperty("TipoDispositivo").ValueKind);
        Assert.Equal(JsonValueKind.Null, e.GetProperty("Fabricante").ValueKind);
        Assert.Equal(JsonValueKind.Null, e.GetProperty("Modelo").ValueKind);
    }

    [Fact]
    public async Task MarcarComoConfiable_SinFila_DevuelveLaEntidadAgregada()
    {
        var ctx = CrearContext();
        var repo = new DispConfiableRepository(ctx);

        var resultado = repo.MarcarComoConfiable(1, 2, 1, "Nuevo", null);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(2, resultado.Value!.IdTenant);
        Assert.True(resultado.Value.Confiable);
        Assert.Equal(EntityState.Added, ctx.Entry(resultado.Value!).State);
    }
}
