using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Tests.Tests.S103;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S102;

/// <summary>
/// S102 · D4-D5 — permisos directos y herencia de roles acotados al tenant: un no-sistema
/// solo lista lo suyo, no reactiva filas de otro tenant y revocar/eliminar fuera de alcance es 404.
/// </summary>
public sealed class S102PermisosAlcanceTests
{
    private const int TenantPropio = 2;
    private const int OtroTenant = 4;

    private readonly Mock<IUnitOfWorkAsync> _uow = new();
    private readonly Mock<IMapper> _mapper = new();

    public S102PermisosAlcanceTests()
    {
        _mapper.Setup(m => m.Map<IReadOnlyList<UsuarioPermisoDto>>(It.IsAny<object>()))
            .Returns((object origen) => [.. ((IEnumerable<UsuarioPermiso>)origen).Select(ADto)]);
        _mapper.Setup(m => m.Map<UsuarioPermisoDto>(It.IsAny<object>()))
            .Returns((object origen) => ADto((UsuarioPermiso)origen));
        _mapper.Setup(m => m.Map<UsuarioPermiso>(It.IsAny<object>()))
            .Returns((object origen) =>
            {
                var d = (CrearUsuarioPermisoDto)origen;
                return new UsuarioPermiso { IdUsuario = d.IdUsuario, IdPermiso = d.IdPermiso, IdTenant = d.IdTenant, IdTipoAsig = d.IdTipoAsig };
            });
    }

    private static UsuarioPermisoDto ADto(UsuarioPermiso up) =>
        new() { Id = up.Id, IdUsuario = up.IdUsuario, IdPermiso = up.IdPermiso, IdTenant = up.IdTenant, Activo = up.Activo };

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync(Action<PassPlatDbContext> sembrar)
    {
        var bd = Guid.NewGuid().ToString();
        await using var semilla = CrearContext(bd);
        sembrar(semilla);
        await semilla.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private UsuarioPermisoService CrearPermisos(PassPlatDbContext ctx) => new(
        new UsuarioPermisoRepository(ctx), new AccesoRepository(ctx), new UsuarioTenantRepository(ctx), new PermisoRepository(ctx),
        new AuthRepository(ctx, _uow.Object), _mapper.Object);

    [Fact]
    public async Task UsuariosPermisos_con_alcance_lista_solo_su_tenant()
    {
        // el repositorio incluye Permiso y Tenant: se simula para no sembrar los catálogos
        var repo = new Mock<IUsuarioPermisoRepository>();
        repo.Setup(r => r.ObtenerPorUsuarioAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<UsuarioPermiso>>.Success(new List<UsuarioPermiso>
            {
                new() { Id = 1, IdUsuario = 7, IdPermiso = 10, IdTenant = TenantPropio },
                new() { Id = 2, IdUsuario = 7, IdPermiso = 11, IdTenant = OtroTenant },
            }));
        var servicio = new UsuarioPermisoService(repo.Object, Mock.Of<IAccesoRepository>(), Mock.Of<IUsuarioTenantRepository>(),
            Mock.Of<IPermisoRepository>(), Mock.Of<IAuthRepository>(), _mapper.Object);
        var ct = TestContext.Current.CancellationToken;

        var propio = await servicio.ObtenerPorUsuarioAsync(7, TenantPropio, ct);
        var sistema = await servicio.ObtenerPorUsuarioAsync(7, null, ct);

        Assert.True(propio.IsSuccess);
        Assert.Equal([1], propio.Value.Select(p => p.Id));
        Assert.Equal(2, sistema.Value.Count);
    }

    [Fact]
    public async Task Conceder_no_reactiva_el_permiso_de_otro_tenant()
    {
        // S103: conceder es ahora AplicarPermisosAsync; sigue sin tocar la fila inactiva del otro tenant
        var bd = await S103PermisosUsuarioEditorTests.SembrarAsync(c =>
        {
            c.Set<Acceso>().Add(S103PermisosUsuarioEditorTests.Acceso(1, 7, TenantPropio, 1));
            c.Set<UsuarioPermiso>().Add(new UsuarioPermiso { Id = 5, IdUsuario = 7, IdPermiso = 2, IdTenant = OtroTenant, Activo = false });
        });
        await using var ctx = CrearContext(bd);

        var result = await CrearPermisos(ctx).AplicarPermisosAsync(
            new AplicarPermisosUsuarioDto { IdUsuario = 7, IdTenant = TenantPropio, IdApp = 1, Cambios = [new CambioPermisoDto { IdPermiso = 2, Tiene = true }] },
            99, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        // la fila del otro tenant ni se carga ni se toca
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.Entity.Id == 5);
        Assert.False(ctx.Set<UsuarioPermiso>().AsNoTracking().Single(up => up.Id == 5).Activo);
        var nueva = ctx.ChangeTracker.Entries<UsuarioPermiso>().Single(e => e.Entity.IdTenant == TenantPropio);
        Assert.Equal(EntityState.Added, nueva.State);
        Assert.Equal(TenantPropio, nueva.Entity.IdTenant);
    }

    [Fact]
    public async Task Revocar_fuera_del_alcance_devuelve_NOT_FOUND()
    {
        var bd = await SembrarAsync(c => c.Set<UsuarioPermiso>().Add(
            new UsuarioPermiso { Id = 3, IdUsuario = 7, IdPermiso = 10, IdTenant = OtroTenant, Activo = true }));
        await using var ctx = CrearContext(bd);

        var result = await CrearPermisos(ctx).RevocarPermisoAsync(3, TenantPropio, 99, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.True(ctx.Set<UsuarioPermiso>().Local.Single().Activo);
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State == EntityState.Modified);
    }

    [Fact]
    public async Task RolesHerencia_eliminar_fuera_del_alcance_devuelve_NOT_FOUND()
    {
        var bd = await SembrarAsync(c => c.Set<RolesHerencia>().Add(
            new RolesHerencia { Id = 8, IdRolHijo = 5, IdRolPadre = 6, IdTenant = OtroTenant }));
        await using var ctx = CrearContext(bd);
        var servicio = new RolesHerenciaService(new RolesHerenciaRepository(ctx), Mock.Of<IRolRepository>(), _uow.Object, Mock.Of<IMapper>());
        var ct = TestContext.Current.CancellationToken;

        var fuera = await servicio.EliminarAsync(8, TenantPropio, ct);

        Assert.True(fuera.IsFailure);
        Assert.Equal(ErrorType.NotFound, fuera.Error!.Type);
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<RolesHerencia>(), e => e.State == EntityState.Deleted);

        var sistema = await servicio.EliminarAsync(8, null, ct);
        Assert.True(sistema.IsSuccess);
    }
}
