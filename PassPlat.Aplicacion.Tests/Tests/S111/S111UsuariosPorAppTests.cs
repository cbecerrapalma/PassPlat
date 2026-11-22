using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S111;

/// <summary>S111 · D5 — el filtro por app exige un Acceso activo en el tenant consultado.</summary>
public sealed class S111UsuariosPorAppTests
{
    private const int Tenant5 = 5;
    private const int Tenant6 = 6;
    private const int App12 = 12;

    private const int U1 = 1;   // t5, Acceso a 12 en t5
    private const int U2 = 2;   // t5, Acceso a 12 solo en t6
    private const int U3 = 3;   // t5, sin Accesos
    private const int U4 = 4;   // t6, Acceso a 12 en t6

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync()
    {
        var bd = Guid.NewGuid().ToString();
        await using var c = CrearContext(bd);
        c.Set<EstadoUsr>().Add(new EstadoUsr { Id = 1, Codigo = "ACTIVO", Nombre = "Activo" });
        c.Set<Tenant>().AddRange(
            new Tenant { Id = Tenant5, Codigo = "T5", Nombre = "Tenant 5" },
            new Tenant { Id = Tenant6, Codigo = "T6", Nombre = "Tenant 6" });
        c.Set<App>().Add(new App { Id = App12, Codigo = "INVENTA", Nombre = "Inventa", Activa = true });
        c.Set<Rol>().Add(new Rol { Id = 1, IdTenant = Tenant5, Codigo = "R1", Nombre = "Rol 1", Activo = true });
        c.Set<Usuario>().AddRange(
            new Usuario { Id = U1, IdTenant = Tenant5, IdEstado = 1, NomUsuario = "u1", Nombre = "U", Apellido = "1" },
            new Usuario { Id = U2, IdTenant = Tenant5, IdEstado = 1, NomUsuario = "u2", Nombre = "U", Apellido = "2" },
            new Usuario { Id = U3, IdTenant = Tenant5, IdEstado = 1, NomUsuario = "u3", Nombre = "U", Apellido = "3" },
            new Usuario { Id = U4, IdTenant = Tenant6, IdEstado = 1, NomUsuario = "u4", Nombre = "U", Apellido = "4" });
        c.Set<Acceso>().AddRange(
            new Acceso { Id = 1, IdUsuario = U1, IdTenant = Tenant5, IdApp = App12, IdRol = 1 },
            new Acceso { Id = 2, IdUsuario = U2, IdTenant = Tenant6, IdApp = App12, IdRol = 1 },
            new Acceso { Id = 3, IdUsuario = U4, IdTenant = Tenant6, IdApp = App12, IdRol = 1 });
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    // tenant 5 + app 12 → solo U1 (su Acceso a 12 está en t5)
    // sin alcance (sistema) + app 12 → U1, U2, U4 (Acceso a 12 en cualquier tenant)
    [Theory]
    [InlineData(Tenant5, new[] { U1 })]
    [InlineData(null, new[] { U1, U2, U4 })]
    public async Task Paginado_con_idApp_exige_Acceso_activo_en_el_tenant(int? idTenant, int[] esperados)
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);
        var repo = new UsuarioRepository(ctx, Mock.Of<IUnitOfWorkAsync>());
        var ct = TestContext.Current.CancellationToken;

        var resultado = await repo.BuscarPaginadoFiltradoAsync(idTenant, App12, null, null, 1, 200, ct);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(esperados, resultado.Value.Items.Select(u => u.Id).Order().ToList());
    }

    // sin idApp no se filtra por app: tenant 5 ve sus 3 usuarios
    [Fact]
    public async Task Sin_idApp_no_filtra_por_app()
    {
        var bd = await SembrarAsync();
        await using var ctx = CrearContext(bd);
        var repo = new UsuarioRepository(ctx, Mock.Of<IUnitOfWorkAsync>());
        var ct = TestContext.Current.CancellationToken;

        var resultado = await repo.BuscarPaginadoFiltradoAsync(Tenant5, null, null, null, 1, 200, ct);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(3, resultado.Value.Items.Count);
    }
}
