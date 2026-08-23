using Microsoft.EntityFrameworkCore;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Test.Tests;

public class UsuarioTenantPersistenceTests
{
    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static async Task<(PassPlatDbContext ctx, EstadoUsr estado, Tenant tenant, Usuario usuario)> SeedBaseAsync()
    {
        var ctx = CrearContext();
        var estado = new EstadoUsr { Id = 1, Codigo = "ACT", Nombre = "Activo" };
        var tenant = new Tenant { Id = 1, Codigo = "TST", Nombre = "Test" };
        var usuario = new Usuario { Id = 1, IdTenant = 1, IdEstado = 1, NomUsuario = "test", Nombre = "Test", Apellido = "User" };
        ctx.EstadosUsr.Add(estado);
        ctx.Tenants.Add(tenant);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        return (ctx, estado, tenant, usuario);
    }

    [Fact]
    public async Task CrearUsuarioTenant_PersisteCorrectamente()
    {
        var (ctx, estado, tenant, usuario) = await SeedBaseAsync();

        var ut = UsuarioTenant.Crear(usuario.Id, tenant.Id, estado.Id);
        ctx.UsuarioTenants.Add(ut);
        await ctx.SaveChangesAsync();

        var cargado = await ctx.UsuarioTenants.FirstOrDefaultAsync(u => u.Id == ut.Id);
        Assert.NotNull(cargado);
        Assert.Equal(usuario.Id, cargado.IdUsuario);
        Assert.Equal(tenant.Id, cargado.IdTenant);
        Assert.Equal(estado.Id, cargado.IdEstado);
        Assert.True(cargado.Activo);
    }

    [Fact]
    public async Task CargarNavegaciones_UsuarioTenant()
    {
        var (ctx, estado, tenant, usuario) = await SeedBaseAsync();

        var ut = UsuarioTenant.Crear(usuario.Id, tenant.Id, estado.Id);
        ctx.UsuarioTenants.Add(ut);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var cargado = await ctx.UsuarioTenants
            .Include(u => u.Usuario)
            .Include(u => u.Tenant)
            .Include(u => u.Estado)
            .FirstOrDefaultAsync(u => u.Id == ut.Id);

        Assert.NotNull(cargado);
        Assert.NotNull(cargado.Usuario);
        Assert.Equal(usuario.NomUsuario, cargado.Usuario!.NomUsuario);
        Assert.NotNull(cargado.Tenant);
        Assert.Equal(tenant.Nombre, cargado.Tenant!.Nombre);
        Assert.NotNull(cargado.Estado);
        Assert.Equal(estado.Nombre, cargado.Estado!.Nombre);
    }

    [Fact]
    public async Task AccesoConIdUsuarioTenant_CargaNavegacionCompuesta()
    {
        var (ctx, estado, tenant, usuario) = await SeedBaseAsync();
        var app = new App { Id = 1, Codigo = "APP", Nombre = "App Test" };
        var rol = new Rol { Id = 1, Nombre = "Admin", IdTenant = tenant.Id };
        ctx.Apps.Add(app);
        ctx.Roles.Add(rol);

        var ut = UsuarioTenant.Crear(usuario.Id, tenant.Id, estado.Id);
        ctx.UsuarioTenants.Add(ut);
        await ctx.SaveChangesAsync();

        var acceso = Acceso.Crear(usuario.Id, tenant.Id, app.Id, rol.Id);
        acceso.IdUsuarioTenant = ut.Id;
        ctx.Accesos.Add(acceso);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var cargado = await ctx.Accesos
            .Include(a => a.UsuarioTenant)
            .FirstOrDefaultAsync(a => a.Id == acceso.Id);

        Assert.NotNull(cargado);
        Assert.Equal(ut.Id, cargado.IdUsuarioTenant);
        Assert.NotNull(cargado.UsuarioTenant);
        Assert.Equal(usuario.Id, cargado.UsuarioTenant!.IdUsuario);
        Assert.Equal(tenant.Id, cargado.UsuarioTenant.IdTenant);
    }

    [Fact]
    public async Task PlatformScope_NullIdUsuarioTenant_Funciona()
    {
        var (ctx, estado, tenant, usuario) = await SeedBaseAsync();
        var app = new App { Id = 1, Codigo = "APP", Nombre = "App Test" };
        var rolPlatform = new Rol { Id = 1, Nombre = "Admin Platform", IdTenant = null };
        ctx.Apps.Add(app);
        ctx.Roles.Add(rolPlatform);

        var acceso = Acceso.Crear(usuario.Id, 0, app.Id, rolPlatform.Id);
        acceso.IdTenant = 1;
        acceso.IdUsuarioTenant = null;
        ctx.Accesos.Add(acceso);
        await ctx.SaveChangesAsync();

        var cargado = await ctx.Accesos
            .Include(a => a.UsuarioTenant)
            .FirstOrDefaultAsync(a => a.Id == acceso.Id);

        Assert.NotNull(cargado);
        Assert.Null(cargado.IdUsuarioTenant);
        Assert.Null(cargado.UsuarioTenant);
    }

    [Fact]
    public void FKCompuesta_TieneDeleteBehaviorRestrict()
    {
        var ctx = CrearContext();
        var fk = ctx.Model.FindEntityType(typeof(Acceso))
            ?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(UsuarioTenant));

        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Restrict, fk!.DeleteBehavior);

        var fkProps = fk.Properties.Select(p => p.Name).ToArray();
        Assert.Contains("IdUsuarioTenant", fkProps);
        Assert.Contains("IdUsuario", fkProps);

        var pkProps = fk.PrincipalKey.Properties.Select(p => p.Name).ToArray();
        Assert.Contains("Id", pkProps);
        Assert.Contains("IdUsuario", pkProps);
    }

    [Fact]
    public void ConfiguracionUnicidad_UsuarioTenant_TieneIndiceUnico()
    {
        var ctx = CrearContext();
        var entityType = ctx.Model.FindEntityType(typeof(UsuarioTenant));
        Assert.NotNull(entityType);

        var hasUniqueIndex = entityType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == "IdUsuario") && i.Properties.Any(p => p.Name == "IdTenant"));

        Assert.True(hasUniqueIndex, "UX_UsuarioTenant_Usuario_Tenant debe existir en el modelo EF");
    }

    [Fact]
    public async Task NavegacionInversa_UsuarioTieneUsuarioTenants()
    {
        var (ctx, estado, tenant, usuario) = await SeedBaseAsync();

        var ut1 = UsuarioTenant.Crear(usuario.Id, tenant.Id, estado.Id);
        ctx.UsuarioTenants.Add(ut1);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var cargado = await ctx.Usuarios
            .Include(u => u.UsuarioTenants)
            .FirstOrDefaultAsync(u => u.Id == usuario.Id);

        Assert.NotNull(cargado);
        Assert.NotEmpty(cargado!.UsuarioTenants);
        Assert.Contains(cargado.UsuarioTenants, ut => ut.IdTenant == tenant.Id);
    }
}
