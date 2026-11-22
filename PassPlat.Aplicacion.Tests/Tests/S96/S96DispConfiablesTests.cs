using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Events;
using CBP.Logging.Interfaces;
using CBP.Outbox;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S96;

/// <summary>
/// S96 · D5 — la lista de dispositivos confiables de un usuario que sí tiene filas en su tenant
/// (reproducción de la fila Id 1 de admin_abarrotes en el tenant 2) devuelve la fila con sus datos.
/// </summary>
public class S96DispConfiablesTests
{
    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    // S136 · D9 — registro con el evento de dispositivo nuevo, como en AplicacionDependencyInjection
    private static OutboxEventRegistry Registro()
    {
        var registro = new OutboxEventRegistry();
        registro.Registrar(typeof(NewDeviceDetectedEvent));
        return registro;
    }

    private static DispConfiableService CrearServicio(PassPlatDbContext ctx) =>
        new(new DispConfiableRepository(ctx), new DispRepository(ctx), new OutboxWriter(new OutboxRepository(ctx, Mock.Of<IUnitOfWorkAsync>()), Registro()),
            Mapper, Mock.Of<IEventPublisher>(), Mock.Of<IAuditoriaPwdRepository>(), new Mock<IHttpContextAccessor>().Object,
            Mock.Of<ILoggerService>(), NullLogger<DispConfiableService>.Instance);

    [Fact]
    public async Task ObtenerPorUsuarioAsync_FilaPropia_DevuelveElDispositivo()
    {
        using var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tenant = new Tenant { Id = 2, Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var tipo = new TipoDisp { Id = 1, Nombre = "Escritorio", EsMovil = false };
        var disp = new Disp { Id = 1, IdTipoDisp = 1, Fabricante = "Fab", Modelo = "Mod", IP = "10.0.0.5", CantidadLogins = 3 };
        var usuario = new Usuario { Id = 3, IdTenant = 2, NomUsuario = "admin_abarrotes" };
        var fila = new DispConfiable
        {
            Id = 1, IdUsuario = 3, IdTenant = 2, IdDisp = 1, Nombre = "CertDisp1", Confiable = true,
            FecAlta = new DateTime(2026, 11, 2, 22, 40, 0), UltUso = new DateTime(2026, 11, 2, 22, 45, 0)
        };

        ctx.Tenants.Add(tenant);
        ctx.TiposDisp.Add(tipo);
        ctx.Disp.Add(disp);
        ctx.Usuarios.Add(usuario);
        ctx.DispConfiables.Add(fila);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CrearServicio(ctx);

        var result = await service.ObtenerPorUsuarioAsync(3, 2, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = Assert.Single(result.Value!);
        Assert.Equal(1, dto.Id);
        Assert.Equal("CertDisp1", dto.Nombre);
        Assert.True(dto.Confiable);
        Assert.Equal("Mod", dto.DispModelo);
        Assert.Equal("Escritorio", dto.DispTipo);
        Assert.Equal("10.0.0.5", dto.IP);
        Assert.Equal(3, dto.CantidadLogins);
        Assert.Equal("admin_abarrotes", dto.UsuarioNombre);
        Assert.Equal(new DateTime(2026, 11, 2, 22, 40, 0), dto.FecAlta);
    }

    /// <summary>S96.1 · D4 — la cancelación del request se propaga en vez de convertirse en DB_ERROR (500).</summary>
    [Fact]
    public async Task ObtenerPorUsuarioAsync_TokenCancelado_PropagaLaCancelacion()
    {
        using var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var service = CrearServicio(ctx);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ObtenerPorUsuarioAsync(3, 2, cts.Token));
    }
}
