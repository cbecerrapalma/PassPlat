using AutoMapper;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Tests.Tests.S61;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S105;

/// <summary>
/// S105 · D3-D4 — los servicios filtran por el alcance de tenant que les pasa el controlador
/// (null = sin acotar) y el repositorio de EmailLog deja pasar la cancelación.
/// </summary>
public sealed class S105AlcanceTenantServiciosTests
{
    private static PassPlatDbContext Contexto() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CancellationToken Cancelado()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    // usuario 5: un Acceso en el tenant 1 y dos en el tenant 2
    private static async Task<AccesoService> AccesoServiceConAccesosEnDosTenantsAsync(PassPlatDbContext ctx)
    {
        ctx.EstadosUsr.Add(new EstadoUsr { Id = 1, Codigo = "ACT", Nombre = "Activo" });
        ctx.Tenants.AddRange(new Tenant { Id = 1, Codigo = "T1", Nombre = "Uno" }, new Tenant { Id = 2, Codigo = "T2", Nombre = "Dos" });
        ctx.Usuarios.Add(new Usuario { Id = 5, IdTenant = 1, IdEstado = 1, NomUsuario = "u5", Nombre = "U", Apellido = "Cinco" });
        ctx.Apps.Add(new App { Id = 1, Codigo = "APP", Nombre = "App" });
        ctx.Roles.Add(new Rol { Id = 1, Nombre = "Rol" });
        ctx.Accesos.AddRange(Acceso.Crear(5, 1, 1, 1), Acceso.Crear(5, 2, 1, 1), Acceso.Crear(5, 2, 1, 1));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IReadOnlyList<AccesoDto>>(It.IsAny<object>()))
            .Returns((object origen) => ((IEnumerable<Acceso>)origen).Select(a => new AccesoDto { Id = a.Id, IdTenant = a.IdTenant }).ToList());

        return new AccesoService(new AccesoRepository(ctx), mapper.Object, Mock.Of<IUsuarioRepository>(), Mock.Of<IRolRepository>(),
            Mock.Of<IUsuarioTenantRepository>(), Mock.Of<IAppRepository>(), Mock.Of<IEmailQueue>(),
            NullLogger<AccesoService>.Instance, new FakeUnitOfWork(ctx), new HttpContextAccessor());
    }

    [Fact]
    public async Task AccesosUsuario_con_alcance_devuelve_solo_su_tenant()
    {
        await using var ctx = Contexto();
        var service = await AccesoServiceConAccesosEnDosTenantsAsync(ctx);

        var result = await service.ObtenerAccesosUsuarioAsync(5, 2, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, a => Assert.Equal(2, a.IdTenant));
    }

    [Fact]
    public async Task AccesosUsuario_sin_alcance_devuelve_todos_los_tenants()
    {
        await using var ctx = Contexto();
        var service = await AccesoServiceConAccesosEnDosTenantsAsync(ctx);

        var result = await service.ObtenerAccesosUsuarioAsync(5, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task EmailLogRepository_propaga_la_cancelacion()
    {
        await using var ctx = Contexto();
        var repo = new EmailLogRepository(ctx);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ObtenerPorUsuarioAsync(1, Cancelado()));
    }
}
