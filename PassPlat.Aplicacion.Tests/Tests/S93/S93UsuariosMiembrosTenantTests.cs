using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S93;

/// <summary>
/// S93 · P53 — el filtro por membresía (idTenantMiembro) usa el mismo criterio que
/// GrupoUsuarioService.AgregarMiembroAsync: UsuarioTenant activo en el tenant, sin importar
/// el tenant de origen del usuario.
/// </summary>
public class S93UsuariosMiembrosTenantTests
{
    private sealed record Semilla(PassPlatDbContext Ctx, int IdTenantAbarrotes);

    private static Semilla Sembrar()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var abarrotes = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var otro = new Tenant { Codigo = "OTRO", Nombre = "Otro Tenant" };
        var estado = new EstadoUsr { Codigo = "ACTIVO", Nombre = "Activo" };
        ctx.Tenants.AddRange(abarrotes, otro);
        ctx.EstadosUsr.Add(estado);
        ctx.SaveChanges();

        // ana: origen y membresía en Abarrotes. bruno: origen Abarrotes sin membresía.
        // carla: origen Otro con membresía activa en Abarrotes. dario: origen Otro con membresía desactivada.
        var ana = Usuario.Crear(abarrotes.Id, estado.Id, "ana.gomez", "ana@abarrotes.test", "Ana", "Gomez");
        var bruno = Usuario.Crear(abarrotes.Id, estado.Id, "bruno.diaz", "bruno@abarrotes.test", "Bruno", "Diaz");
        var carla = Usuario.Crear(otro.Id, estado.Id, "carla.ruiz", "carla@otro.test", "Carla", "Ruiz");
        var dario = Usuario.Crear(otro.Id, estado.Id, "dario.paz", "dario@otro.test", "Dario", "Paz");
        ctx.Usuarios.AddRange(ana, bruno, carla, dario);
        ctx.SaveChanges();

        var membresiaDario = UsuarioTenant.Crear(dario.Id, abarrotes.Id, estado.Id);
        ctx.UsuarioTenants.AddRange(
            UsuarioTenant.Crear(ana.Id, abarrotes.Id, estado.Id),
            UsuarioTenant.Crear(carla.Id, abarrotes.Id, estado.Id),
            membresiaDario);
        ctx.SaveChanges();
        membresiaDario.Desactivar();
        ctx.SaveChanges();

        return new Semilla(ctx, abarrotes.Id);
    }

    [Fact]
    public async Task BuscarPaginadoFiltrado_por_miembro_devuelve_las_membresias_activas_sin_importar_el_origen()
    {
        var semilla = Sembrar();
        var repo = new UsuarioRepository(semilla.Ctx, new Mock<IUnitOfWorkAsync>().Object);

        var result = await repo.BuscarPaginadoFiltradoAsync(null, null, semilla.IdTenantAbarrotes, null, 1, 10, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var (items, total) = result.Value;
        Assert.Equal(2, total);
        Assert.Equal(new[] { "ana.gomez", "carla.ruiz" }, items.Select(u => u.NomUsuario).ToArray());
    }

    [Fact]
    public async Task BuscarPaginadoFiltrado_por_miembro_combina_con_la_busqueda()
    {
        var semilla = Sembrar();
        var repo = new UsuarioRepository(semilla.Ctx, new Mock<IUnitOfWorkAsync>().Object);

        var result = await repo.BuscarPaginadoFiltradoAsync(null, null, semilla.IdTenantAbarrotes, "carla", 1, 10, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var usuario = Assert.Single(result.Value.Items);
        Assert.Equal("carla.ruiz", usuario.NomUsuario);
        Assert.Equal(1, result.Value.TotalCount);
    }
}
