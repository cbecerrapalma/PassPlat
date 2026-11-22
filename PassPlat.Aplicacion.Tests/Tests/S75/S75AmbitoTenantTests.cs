using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
using CBP.Logging.Interfaces;
using CBP.Outbox;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Models;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S75;

/// <summary>
/// S75 — Lecturas de dispositivos, estadísticas de federación y conteo de usuarios
/// acotadas al tenant activo; idTenant null = modo Plataforma (sin filtro).
/// </summary>
public class S75AmbitoTenantTests
{
    private static PassPlatDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DispConfiableService CrearDispService(PassPlatDbContext ctx)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IReadOnlyList<DispConfiableDto>>(It.IsAny<object>()))
            .Returns((object src) => ((IEnumerable<DispConfiable>)src)
                .Select(d => new DispConfiableDto { Id = d.Id, IdUsuario = d.IdUsuario, IdTenant = d.IdTenant, IdDisp = d.IdDisp })
                .ToList());
        var auditoria = new Mock<IAuditoriaPwdRepository>();
        auditoria.Setup(a => a.RegistrarAuditoria(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Failure("AUD", "sin auditoria"));
        return new DispConfiableService(
            new DispConfiableRepository(ctx), new DispRepository(ctx), Mock.Of<IOutboxWriter>(), mapper.Object, Mock.Of<IEventPublisher>(), auditoria.Object,
            Mock.Of<IHttpContextAccessor>(), Mock.Of<ILoggerService>(), NullLogger<DispConfiableService>.Instance);
    }

    // Filas: Id 1 y 2 en tenant 1 (usuario 1), Id 3 en tenant 2 (usuario 1)
    private static async Task SembrarDispositivosAsync(PassPlatDbContext ctx)
    {
        var tipo = new TipoDisp { Id = 1, Nombre = "PC" };
        ctx.Add(tipo);
        ctx.Add(new Disp { Id = 1, IdTipoDisp = 1, TipoDisp = tipo });
        ctx.Add(new Disp { Id = 2, IdTipoDisp = 1, TipoDisp = tipo });
        ctx.Usuarios.Add(new Usuario { Id = 1, IdTenant = 1, IdEstado = 1, NomUsuario = "u1", Nombre = "U", Apellido = "Uno" });
        ctx.DispConfiables.AddRange(
            new DispConfiable { Id = 1, IdUsuario = 1, IdTenant = 1, IdDisp = 1, Confiable = true },
            new DispConfiable { Id = 2, IdUsuario = 1, IdTenant = 1, IdDisp = 2, Confiable = true },
            new DispConfiable { Id = 3, IdUsuario = 1, IdTenant = 2, IdDisp = 1, Confiable = true });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task DispConfiable_ObtenerTodos_ConTenant_SoloDevuelveEseTenant()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);

        var result = await CrearDispService(ctx).ObtenerTodosConDispositivoAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, d => Assert.Equal(1, d.IdTenant));
    }

    [Fact]
    public async Task DispConfiable_ObtenerTodos_SinTenant_DevuelveTodos()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);

        var result = await CrearDispService(ctx).ObtenerTodosConDispositivoAsync(null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task DispConfiable_ObtenerPorUsuario_ConTenant_FiltraTenant()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);
        var service = CrearDispService(ctx);

        var tenant2 = await service.ObtenerPorUsuarioAsync(1, 2, TestContext.Current.CancellationToken);
        var todos = await service.ObtenerPorUsuarioAsync(1, null, TestContext.Current.CancellationToken);

        Assert.True(tenant2.IsSuccess);
        Assert.Equal(3, Assert.Single(tenant2.Value).Id);
        Assert.Equal(3, todos.Value.Count);
    }

    [Fact]
    public async Task DispConfiable_Eliminar_DeOtroTenant_DevuelveDispNotFound_YNoBorra()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);

        var result = await CrearDispService(ctx).EliminarAsync(3, 1, TestContext.Current.CancellationToken);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("DISP_NOT_FOUND", result.Error!.Code);
        Assert.True(await ctx.DispConfiables.AnyAsync(d => d.Id == 3, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispConfiable_Bloquear_DeOtroTenant_DevuelveDispNotFound_YNoCambia()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);

        var result = await CrearDispService(ctx).BloquearAsync(3, 1, TestContext.Current.CancellationToken);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        Assert.True(result.IsFailure);
        Assert.Equal("DISP_NOT_FOUND", result.Error!.Code);
        var entidad = await ctx.DispConfiables.SingleAsync(d => d.Id == 3, TestContext.Current.CancellationToken);
        Assert.True(entidad.Confiable);
    }

    [Fact]
    public async Task DispConfiable_Eliminar_MismoTenant_MarcaParaBorrar()
    {
        var ctx = CrearContext();
        await SembrarDispositivosAsync(ctx);

        var result = await CrearDispService(ctx).EliminarAsync(1, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var entrada = Assert.Single(ctx.ChangeTracker.Entries<DispConfiable>(), e => e.Entity.Id == 1);
        Assert.Equal(EntityState.Deleted, entrada.State);
    }
}
