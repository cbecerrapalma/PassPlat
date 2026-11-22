using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S61;

/// <summary>
/// S61 - K4 (D3): el scope de plataforma exige involucrado global y membresia activa en
/// el tenant EsSistema, y sus permisos salen solo de roles globales.
/// </summary>
public class S61AuthRepositoryTests
{
    private static (AuthRepository repo, PassPlatDbContext ctx) Crear()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new PassPlatDbContext(options);
        return (new AuthRepository(ctx, Mock.Of<IUnitOfWorkAsync>()), ctx);
    }

    private static void Sembrar(PassPlatDbContext ctx)
    {
        ctx.Tenants.AddRange(
            new Tenant { Id = 1, Codigo = "PLATFORM", Activo = true, EsSistema = true },
            new Tenant { Id = 2, Codigo = "ABARROTES", Activo = true, EsSistema = false });

        var rolGlobal = new Rol { Id = 1, Codigo = "PLATFORM_ADMIN", Activo = true, IdTenant = null };
        var rolTenant = new Rol { Id = 5, Codigo = "ABARROTES_ADMIN", Activo = true, IdTenant = 2 };
        ctx.Roles.AddRange(rolGlobal, rolTenant);

        var permisoGlobal = new Permiso { Id = 100, Codigo = "USUARIOS_VER", Activo = true };
        var permisoTenant = new Permiso { Id = 101, Codigo = "ABARROTES_VER", Activo = true };
        ctx.Permisos.AddRange(permisoGlobal, permisoTenant);

        ctx.RolesPermisos.AddRange(
            new RolPermiso { Id = 1, IdRol = 1, IdPermiso = 100, Activo = true, Rol = rolGlobal, Permiso = permisoGlobal },
            new RolPermiso { Id = 2, IdRol = 5, IdPermiso = 101, Activo = true, Rol = rolTenant, Permiso = permisoTenant });

        ctx.UsuarioTenants.AddRange(
            new UsuarioTenant { Id = 21, IdUsuario = 9, IdTenant = 1, Activo = true },
            new UsuarioTenant { Id = 22, IdUsuario = 10, IdTenant = 2, Activo = true });

        ctx.SaveChanges();
    }

    private static Acceso Acceso(int id, int idUsuario, int idApp, int idRol, bool activo = true, int? idUsuarioTenant = null)
        => new()
        {
            Id = id,
            IdUsuario = idUsuario,
            IdApp = idApp,
            IdTenant = 1,
            IdRol = idRol,
            IdUsuarioTenant = idUsuarioTenant,
            Activo = activo,
            FecAsignacion = DateTime.Now
        };

    [Fact]
    public async Task Usuario_de_platform_con_rol_global_tiene_acceso_de_plataforma()
    {
        var (repo, ctx) = Crear();
        Sembrar(ctx);
        ctx.Accesos.Add(Acceso(1, 9, 12, 1));
        ctx.SaveChanges();

        var result = await repo.ExisteAccesoPlatformActivoAsync(9, 12, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task Usuario_sin_membresia_en_platform_no_tiene_acceso_de_plataforma()
    {
        var (repo, ctx) = Crear();
        Sembrar(ctx);
        ctx.Accesos.Add(Acceso(1, 10, 12, 1));
        ctx.SaveChanges();

        var result = await repo.ExisteAccesoPlatformActivoAsync(10, 12, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task Rol_de_tenant_con_IdUsuarioTenant_NULL_no_da_acceso_de_plataforma()
    {
        var (repo, ctx) = Crear();
        Sembrar(ctx);
        ctx.Accesos.Add(Acceso(1, 9, 12, 5));
        ctx.SaveChanges();

        var result = await repo.ExisteAccesoPlatformActivoAsync(9, 12, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task Permisos_de_plataforma_solo_salen_de_roles_globales()
    {
        var (repo, ctx) = Crear();
        Sembrar(ctx);
        ctx.Accesos.AddRange(Acceso(1, 9, 12, 1), Acceso(2, 9, 12, 5));
        ctx.SaveChanges();

        var result = await repo.ObtenerCodigosPermisosPlatformAsync(9, 12, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "USUARIOS_VER" }, result.Value);
    }
}
