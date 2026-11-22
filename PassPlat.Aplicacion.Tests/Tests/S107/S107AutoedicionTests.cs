using System.Linq.Expressions;
using System.Security.Claims;
using AutoMapper;
using CBP.Data.Abstractions;
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
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using Editor = PassPlat.Aplicacion.Tests.Tests.S103.S103PermisosUsuarioEditorTests;

namespace PassPlat.Aplicacion.Tests.Tests.S107;

/// <summary>
/// S107 · D1-D3 (P67) — AUTOEDICION: nadie (salvo el usuario de sistema) cambia sus propios permisos directos,
/// sus propios accesos ni los permisos de un rol que tiene asignado.
/// </summary>
public sealed class S107AutoedicionTests
{
    private const int Usuario = Editor.Usuario;
    private const int Tenant = Editor.Tenant;
    private const int App = Editor.App;
    private const int IdRol = 9;
    private const int Ejecutor = 4;

    private readonly Mock<IRolPermisoRepository> _rolPermisos = new();
    private readonly Mock<IAccesoRepository> _accesos = new();

    private static PassPlatDbContext CrearContext(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static AplicarPermisosUsuarioDto Dar(int idUsuario, int idPermiso) => new()
    {
        IdUsuario = idUsuario,
        IdTenant = Tenant,
        IdApp = App,
        Cambios = [new CambioPermisoDto { IdPermiso = idPermiso, Tiene = true }]
    };

    private static AccesoService ServicioAccesos(PassPlatDbContext ctx, params Claim[] claims) =>
        new(new AccesoRepository(ctx), Mock.Of<IMapper>(), Mock.Of<IUsuarioRepository>(), Mock.Of<IRolRepository>(),
            Mock.Of<IUsuarioTenantRepository>(), Mock.Of<IAppRepository>(), Mock.Of<IEmailQueue>(),
            NullLogger<AccesoService>.Instance, Mock.Of<IUnitOfWorkAsync>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) } });

    private static async Task<PassPlatDbContext> ConAccesoAsync()
    {
        var ctx = CrearContext(Guid.NewGuid().ToString());
        ctx.Set<Acceso>().Add(Acceso.Crear(Usuario, Tenant, App, 10));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return ctx;
    }

    private RolPermisoService ServicioRol(bool tieneElRol)
    {
        var roles = new Mock<IRolRepository>();
        roles.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Rol, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Rol?>.Success(new Rol { Id = IdRol, Nombre = "Admin", IdTenant = Tenant, Activo = true }, allowNull: true));
        var permisos = new Mock<IPermisoRepository>();
        permisos.Setup(p => p.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Permiso>>.Success([new Permiso { Id = 1 }]));
        _rolPermisos.Setup(r => r.WhereAsync(It.IsAny<Expression<Func<RolPermiso, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RolPermiso>>.Success([]));
        _rolPermisos.Setup(r => r.ObtenerActivoPorRolPermisoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RolPermiso?>.Success(null, allowNull: true));
        _rolPermisos.Setup(r => r.Add(It.IsAny<RolPermiso>())).Returns((RolPermiso e) => Result<RolPermiso>.Success(e));
        _accesos.Setup(a => a.AnyAsync(It.IsAny<Expression<Func<Acceso, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(tieneElRol));
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<RolPermiso>(It.IsAny<object>()))
            .Returns((object o) => new RolPermiso { IdRol = ((AsignarPermisoDto)o).IdRol, IdPermiso = ((AsignarPermisoDto)o).IdPermiso });
        mapper.Setup(m => m.Map<RolPermisoDto>(It.IsAny<object>()))
            .Returns((object o) => new RolPermisoDto { IdRol = ((RolPermiso)o).IdRol, IdPermiso = ((RolPermiso)o).IdPermiso });
        return new RolPermisoService(_rolPermisos.Object, roles.Object, permisos.Object, _accesos.Object, mapper.Object);
    }

    [Fact]
    public async Task Aplicar_permisos_propios_devuelve_AUTOEDICION_sin_escribir()
    {
        var bd = await Editor.SembrarAsync(c => c.Set<Acceso>().Add(Editor.Acceso(1, Usuario, Tenant, App)));
        await using var ctx = CrearContext(bd);

        var result = await Editor.CrearServicio(ctx, Mock.Of<IUnitOfWorkAsync>())
            .AplicarPermisosAsync(Dar(Usuario, 2), Usuario, false, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTOEDICION", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State is EntityState.Added or EntityState.Modified);
    }

    [Fact]
    public async Task Aplicar_permisos_propios_como_sistema_se_aplica()
    {
        var bd = await Editor.SembrarAsync(c => c.Set<Acceso>().Add(Editor.Acceso(1, Usuario, Tenant, App)));
        await using var ctx = CrearContext(bd);

        var result = await Editor.CrearServicio(ctx, Mock.Of<IUnitOfWorkAsync>())
            .AplicarPermisosAsync(Dar(Usuario, 2), Usuario, true, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task Revocar_permiso_propio_devuelve_AUTOEDICION_y_el_de_otro_se_revoca()
    {
        var bd = await Editor.SembrarAsync(c => c.Set<UsuarioPermiso>().Add(
            new UsuarioPermiso { Id = 30, IdUsuario = Usuario, IdPermiso = 2, IdTenant = Tenant, IdApp = App, IdTipoAsig = 2, Activo = true }));
        await using var ctx = CrearContext(bd);
        var servicio = Editor.CrearServicio(ctx, Mock.Of<IUnitOfWorkAsync>());
        var ct = TestContext.Current.CancellationToken;

        var propio = await servicio.RevocarPermisoAsync(30, Tenant, Usuario, ct);

        Assert.True(propio.IsFailure);
        Assert.Equal("AUTOEDICION", propio.Error!.Code);
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.State == EntityState.Modified);

        var deOtro = await servicio.RevocarPermisoAsync(30, Tenant, 99, ct);

        Assert.True(deOtro.IsSuccess);
        Assert.Contains(ctx.ChangeTracker.Entries<UsuarioPermiso>(), e => e.Entity.Id == 30 && e.State == EntityState.Modified);
    }

    [Fact]
    public async Task Asignar_y_revocar_acceso_propio_devuelven_AUTOEDICION()
    {
        await using var ctx = await ConAccesoAsync();
        var servicio = ServicioAccesos(ctx, new Claim(ClaimTypes.NameIdentifier, Usuario.ToString()));
        var ct = TestContext.Current.CancellationToken;

        var asignar = await servicio.AsignarAccesoAsync(new AsignarAccesoDto { IdUsuario = Usuario, IdTenant = Tenant, IdApp = App, IdRol = 10 }, ct);
        var revocar = await servicio.RevocarAccesoAsync(Usuario, App, ct);

        Assert.Equal("AUTOEDICION", asignar.Error?.Code);
        Assert.Equal("AUTOEDICION", revocar.Error?.Code);
        Assert.True(ctx.Set<Acceso>().Local.Single().Activo);
    }

    [Fact]
    public async Task Revocar_acceso_de_otro_o_como_sistema_no_es_autoedicion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var ctx = await ConAccesoAsync())
        {
            var deOtro = await ServicioAccesos(ctx, new Claim(ClaimTypes.NameIdentifier, "99")).RevocarAccesoAsync(Usuario, App, ct);
            Assert.True(deOtro.IsSuccess);
        }

        await using (var ctx = await ConAccesoAsync())
        {
            var sistema = await ServicioAccesos(ctx, new Claim(ClaimTypes.NameIdentifier, Usuario.ToString()), new Claim("is_system", "true"))
                .RevocarAccesoAsync(Usuario, App, ct);
            Assert.True(sistema.IsSuccess);
        }
    }

    [Fact]
    public async Task Rol_propio_devuelve_AUTOEDICION_al_aplicar_asignar_y_desasignar()
    {
        var servicio = ServicioRol(tieneElRol: true);
        var ct = TestContext.Current.CancellationToken;

        var aplicar = await servicio.AplicarPermisosAsync(IdRol, [new CambioPermisoDto { IdPermiso = 1, Tiene = true }], Tenant, Ejecutor, ct);
        var asignar = await servicio.AsignarPermisoAsync(new AsignarPermisoDto { IdRol = IdRol, IdPermiso = 1 }, Tenant, Ejecutor, ct);
        var desasignar = await servicio.DesasignarPermisoAsync(IdRol, 1, Tenant, Ejecutor, ct);

        Assert.Equal("AUTOEDICION", aplicar.Error?.Code);
        Assert.Equal("AUTOEDICION", asignar.Error?.Code);
        Assert.Equal("AUTOEDICION", desasignar.Error?.Code);
        _rolPermisos.Verify(r => r.Add(It.IsAny<RolPermiso>()), Times.Never);
        _rolPermisos.Verify(r => r.Update(It.IsAny<RolPermiso>()), Times.Never);
    }

    [Fact]
    public async Task Rol_ajeno_o_sistema_se_edita()
    {
        var ct = TestContext.Current.CancellationToken;

        var ajeno = await ServicioRol(tieneElRol: false).AplicarPermisosAsync(IdRol, [new CambioPermisoDto { IdPermiso = 1, Tiene = true }], Tenant, Ejecutor, ct);
        Assert.True(ajeno.IsSuccess);

        // el usuario de sistema (alcance null) no pasa por la regla: ni siquiera consulta sus accesos
        _accesos.Invocations.Clear();
        var sistema = await ServicioRol(tieneElRol: true).AplicarPermisosAsync(IdRol, [new CambioPermisoDto { IdPermiso = 1, Tiene = true }], null, Ejecutor, ct);
        Assert.True(sistema.IsSuccess);
        _accesos.Verify(a => a.AnyAsync(It.IsAny<Expression<Func<Acceso, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
