using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S116;

/// <summary>S116 · D4 — <c>POST api/sesiones/{id}/revocar</c>: el servicio guarda la revocación.</summary>
public sealed class S116SesionesRevocarTests
{
    [Fact]
    public async Task RevocarSesion_marca_inactiva_y_guarda_una_vez()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var id = Guid.NewGuid();
        ctx.Sesiones.Add(new Sesion { Id = id, IdUsuario = 5, IdTenant = 1, IdApp = 1, IdTokenExt = "t1", HashRefresh = "h", FecExpira = DateTime.Now.AddHours(1), EsActiva = true });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var uow = new Mock<IUnitOfWorkAsync>();
        var service = new SesionService(
            new SesionRepository(ctx, uow.Object, NullLogger<SesionRepository>.Instance),
            Mock.Of<IMapper>(), Mock.Of<IUsuarioTenantRepository>(), uow.Object);

        var r = await service.RevocarSesionAsync(id, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        var sesion = ctx.Sesiones.Single(s => s.Id == id);
        Assert.False(sesion.EsActiva);
        Assert.Null(sesion.HashRefresh);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
