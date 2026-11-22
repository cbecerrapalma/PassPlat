using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S85;

/// <summary>
/// S85 · D5 — filtro de usuarios por tenant y por app (acceso activo) en el servidor,
/// y datos de tenant/estado/apps en <see cref="UsuarioDto"/>.
/// </summary>
public class S85UsuariosFiltroTests
{
    private sealed record Semilla(PassPlatDbContext Ctx, int IdTenantAbarrotes, int IdTenantOtro, int IdAppInventaStore, int IdAppPassPlat);

    private static PassPlatDbContext CrearContext()
        => new(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UsuarioRepository CrearRepo(PassPlatDbContext ctx)
        => new(ctx, new Mock<IUnitOfWorkAsync>().Object);

    private static Semilla Sembrar()
    {
        var ctx = CrearContext();

        var tenantAbarrotes = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var tenantOtro = new Tenant { Codigo = "OTRO", Nombre = "Otro Tenant" };
        var estadoActivo = new EstadoUsr { Codigo = "ACTIVO", Nombre = "Activo" };
        var appInventaStore = new App { Codigo = "INVENTA_STORE", Nombre = "Inventa Store" };
        var appPassPlat = new App { Codigo = "PASSPLAT", Nombre = "PassPlat" };
        ctx.Tenants.AddRange(tenantAbarrotes, tenantOtro);
        ctx.EstadosUsr.Add(estadoActivo);
        ctx.Apps.AddRange(appInventaStore, appPassPlat);
        ctx.SaveChanges();

        var usuarioAna = Usuario.Crear(tenantAbarrotes.Id, estadoActivo.Id, "ana.gomez", "ana@abarrotes.test", "Ana", "Gomez");
        var usuarioBruno = Usuario.Crear(tenantAbarrotes.Id, estadoActivo.Id, "bruno.diaz", "bruno@abarrotes.test", "Bruno", "Diaz");
        var usuarioAnaOtro = Usuario.Crear(tenantOtro.Id, estadoActivo.Id, "ana.otro", "ana@otro.test", "Ana", "Otro");
        ctx.Usuarios.AddRange(usuarioAna, usuarioBruno, usuarioAnaOtro);
        ctx.SaveChanges();

        var accesoAna = Acceso.Crear(usuarioAna.Id, tenantAbarrotes.Id, appInventaStore.Id, 1);
        var accesoBruno = Acceso.Crear(usuarioBruno.Id, tenantAbarrotes.Id, appInventaStore.Id, 1);
        var accesoAnaOtro = Acceso.Crear(usuarioAnaOtro.Id, tenantOtro.Id, appPassPlat.Id, 1);
        ctx.Accesos.AddRange(accesoAna, accesoBruno, accesoAnaOtro);
        ctx.SaveChanges();

        // Bruno conserva el acceso pero revocado: no debe aparecer al filtrar por esa app.
        accesoBruno.Revocar();
        ctx.SaveChanges();

        return new Semilla(ctx, tenantAbarrotes.Id, tenantOtro.Id, appInventaStore.Id, appPassPlat.Id);
    }

    [Fact]
    public async Task BuscarPaginadoFiltrado_por_app_devuelve_solo_usuarios_con_acceso_activo()
    {
        var semilla = Sembrar();
        var repo = CrearRepo(semilla.Ctx);

        var result = await repo.BuscarPaginadoFiltradoAsync(null, semilla.IdAppInventaStore, null, null, 1, 10, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var (items, total) = result.Value;
        Assert.Equal(1, total);
        var usuario = Assert.Single(items);
        Assert.Equal("ana.gomez", usuario.NomUsuario);
    }

    [Fact]
    public async Task BuscarPaginadoFiltrado_por_tenant_y_busqueda_combina_filtros()
    {
        var semilla = Sembrar();
        var repo = CrearRepo(semilla.Ctx);

        var result = await repo.BuscarPaginadoFiltradoAsync(semilla.IdTenantAbarrotes, null, null, "ana", 1, 10, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var (items, total) = result.Value;
        Assert.Equal(1, total);
        var usuario = Assert.Single(items);
        Assert.Equal("ana.gomez", usuario.NomUsuario);
        Assert.Equal(semilla.IdTenantAbarrotes, usuario.IdTenant);
    }

    [Fact]
    public async Task BuscarPaginadoFiltrado_sin_filtros_devuelve_todos_con_total()
    {
        var semilla = Sembrar();
        var repo = CrearRepo(semilla.Ctx);

        var result = await repo.BuscarPaginadoFiltradoAsync(null, null, null, null, 1, 10, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var (items, total) = result.Value;
        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
        Assert.Equal(new[] { "ana.gomez", "ana.otro", "bruno.diaz" }, items.Select(u => u.NomUsuario).ToArray());
    }

    [Fact]
    public async Task Mapeo_UsuarioDto_trae_tenant_y_codigos_de_apps_activas_distintos()
    {
        var ctx = CrearContext();
        var tenant = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var estado = new EstadoUsr { Codigo = "ACTIVO", Nombre = "Activo" };
        var appA = new App { Codigo = "A", Nombre = "App A" };
        var appB = new App { Codigo = "B", Nombre = "App B" };
        var appC = new App { Codigo = "C", Nombre = "App C" };
        ctx.Tenants.Add(tenant);
        ctx.EstadosUsr.Add(estado);
        ctx.Apps.AddRange(appA, appB, appC);
        ctx.SaveChanges();

        var usuario = Usuario.Crear(tenant.Id, estado.Id, "carla.ruiz", "carla@abarrotes.test", "Carla", "Ruiz");
        ctx.Usuarios.Add(usuario);
        ctx.SaveChanges();

        ctx.Accesos.AddRange(
            Acceso.Crear(usuario.Id, tenant.Id, appA.Id, 1),
            Acceso.Crear(usuario.Id, tenant.Id, appA.Id, 2),
            Acceso.Crear(usuario.Id, tenant.Id, appB.Id, 1));
        var accesoInactivo = Acceso.Crear(usuario.Id, tenant.Id, appC.Id, 1);
        ctx.Accesos.Add(accesoInactivo);
        ctx.SaveChanges();
        accesoInactivo.Revocar();
        ctx.SaveChanges();

        var repo = CrearRepo(ctx);
        var paginado = await repo.BuscarPaginadoFiltradoAsync(tenant.Id, null, null, null, 1, 10, TestContext.Current.CancellationToken);
        Assert.True(paginado.IsSuccess);
        var entidad = Assert.Single(paginado.Value.Items);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var dto = mapper.Map<UsuarioDto>(entidad);

        Assert.Equal("Abarrotes del Sur", dto.TenantNombre);
        Assert.Equal("Activo", dto.EstadoNombre);
        Assert.Equal(new[] { "A", "B" }, dto.Apps.ToArray());
    }
}
