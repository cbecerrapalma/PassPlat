using AutoMapper;
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

namespace PassPlat.Aplicacion.Tests.Tests.S79;

/// <summary>
/// S79 — La confianza de un dispositivo es por tenant: consultar, revocar, marcar y detectar
/// por (IdUsuario, IdDisp) se acotan al tenant; idTenant null = modo Plataforma (sin filtro).
/// </summary>
public class S79ConfianzaTenantTests
{
    private static PassPlatDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DispConfiableService CrearService(PassPlatDbContext ctx, Mock<IEventPublisher> publisher)
    {
        var auditoria = new Mock<IAuditoriaPwdRepository>();
        auditoria.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Failure("AUD", "sin auditoria"));
        return new DispConfiableService(
            new DispConfiableRepository(ctx), new DispRepository(ctx), Mock.Of<IOutboxWriter>(), Mock.Of<IMapper>(), publisher.Object, auditoria.Object,
            Mock.Of<IHttpContextAccessor>(), Mock.Of<ILoggerService>(), NullLogger<DispConfiableService>.Instance);
    }

    // Semilla común: Disp 1 y Usuario 1; las filas de DispConfiables las define cada prueba
    private static async Task SembrarAsync(PassPlatDbContext ctx, params DispConfiable[] filas)
    {
        var tipo = new TipoDisp { Id = 1, Nombre = "PC" };
        ctx.Add(tipo);
        ctx.Add(new Disp { Id = 1, IdTipoDisp = 1, TipoDisp = tipo });
        ctx.Usuarios.Add(new Usuario { Id = 1, IdTenant = 1, IdEstado = 1, NomUsuario = "u1", Nombre = "U", Apellido = "Uno" });
        ctx.DispConfiables.AddRange(filas);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    private static DispConfiable Fila(int id, int idTenant, bool confiable) =>
        new() { Id = id, IdUsuario = 1, IdTenant = idTenant, IdDisp = 1, Nombre = $"PC t{idTenant}", Confiable = confiable };

    private static async Task GuardarAsync(PassPlatDbContext ctx)
    {
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    private static DispConfiable FilaDeTenant(PassPlatDbContext ctx, int idTenant) =>
        ctx.DispConfiables.AsNoTracking().Single(d => d.IdUsuario == 1 && d.IdDisp == 1 && d.IdTenant == idTenant);

    [Fact]
    public async Task EsConfiable_ConTenant_NoUsaLaFilaDeOtroTenant()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(1, 2, true));
        var service = CrearService(ctx, new Mock<IEventPublisher>());

        var enTenant1 = await service.EsConfiableAsync(1, 1, 1, TestContext.Current.CancellationToken);
        var enTenant2 = await service.EsConfiableAsync(1, 1, 2, TestContext.Current.CancellationToken);

        Assert.True(enTenant1.IsSuccess);
        Assert.False(enTenant1.Value);
        Assert.True(enTenant2.Value);
    }

    [Fact]
    public async Task Revocar_ConTenant_SoloRevocaEseTenant()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(1, 1, true), Fila(2, 2, true));
        var publisher = new Mock<IEventPublisher>();

        var result = await CrearService(ctx, publisher).RevocarConfianzaAsync(1, 1, 1, TestContext.Current.CancellationToken);
        await GuardarAsync(ctx);

        Assert.True(result.IsSuccess);
        Assert.False(FilaDeTenant(ctx, 1).Confiable);
        Assert.True(FilaDeTenant(ctx, 2).Confiable);
        publisher.Verify(p => p.PublishAsync(It.Is<DeviceRevokedEvent>(e => e.IdTenant == 1), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.PublishAsync(It.IsAny<DeviceRevokedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revocar_SinTenant_RevocaTodosLosTenants()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(1, 1, true), Fila(2, 2, true));
        var publisher = new Mock<IEventPublisher>();

        var result = await CrearService(ctx, publisher).RevocarConfianzaAsync(1, 1, null, TestContext.Current.CancellationToken);
        await GuardarAsync(ctx);

        Assert.True(result.IsSuccess);
        Assert.False(FilaDeTenant(ctx, 1).Confiable);
        Assert.False(FilaDeTenant(ctx, 2).Confiable);
        publisher.Verify(p => p.PublishAsync(It.IsAny<DeviceRevokedEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Revocar_ConTenant_SinFilaEnEseTenant_DevuelveDispNotFound()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(2, 2, true));
        var publisher = new Mock<IEventPublisher>();

        var result = await CrearService(ctx, publisher).RevocarConfianzaAsync(1, 1, 1, TestContext.Current.CancellationToken);
        await GuardarAsync(ctx);

        Assert.True(result.IsFailure);
        Assert.Equal("DISP_NOT_FOUND", result.Error!.Code);
        Assert.True(FilaDeTenant(ctx, 2).Confiable);
        publisher.Verify(p => p.PublishAsync(It.IsAny<DeviceRevokedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarcarComoConfiable_EnOtroTenant_CreaFilaPropia()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(1, 1, false));

        var result = await CrearService(ctx, new Mock<IEventPublisher>())
            .MarcarComoConfiableAsync(1, 2, 1, "PC t2", null, TestContext.Current.CancellationToken);
        await GuardarAsync(ctx);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, ctx.DispConfiables.Count());
        Assert.False(FilaDeTenant(ctx, 1).Confiable);
        Assert.True(FilaDeTenant(ctx, 2).Confiable);
    }

    [Fact]
    public async Task DetectarNuevo_ConfiableEnOtroTenant_CreaFilaDelTenant()
    {
        var ctx = CrearContext();
        await SembrarAsync(ctx, Fila(1, 1, true));

        var result = await CrearService(ctx, new Mock<IEventPublisher>())
            .DetectarNuevoDispositivoAsync(1, 2, 1, "PC t2", null, "10.0.0.1", "agente", TestContext.Current.CancellationToken);
        await GuardarAsync(ctx);

        Assert.True(result.IsSuccess);
        Assert.True(FilaDeTenant(ctx, 2).Confiable);
        var filaT1 = FilaDeTenant(ctx, 1);
        Assert.True(filaT1.Confiable);
        Assert.Equal("PC t1", filaT1.Nombre);
    }
}
