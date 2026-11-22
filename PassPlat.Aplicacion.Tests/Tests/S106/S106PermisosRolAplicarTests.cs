using System.Linq.Expressions;
using AutoMapper;
using CBP.Results;
using CBP.Results.Errors;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S106;

/// <summary>
/// S106 · D1-D2 — aplicar los cambios de permisos de un rol: todo o nada, sin commit propio y acotado al tenant.
/// El commit es del controlador (AGENTS §6): el servicio solo marca Add/Update en el repositorio.
/// </summary>
public sealed class S106PermisosRolAplicarTests
{
    private const int IdRol = 12;
    private const int Ejecutor = 9;

    private readonly Mock<IRolPermisoRepository> _repo = new();
    private readonly Mock<IRolRepository> _roles = new();
    private readonly Mock<IPermisoRepository> _permisos = new();
    private readonly Mock<IAccesoRepository> _accesos = new();

    private RolPermisoService Servicio(Rol? rol, int[] permisosActivos, params RolPermiso[] filasActivas)
    {
        _roles.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Rol, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Rol?>.Success(rol, allowNull: true));
        _permisos.Setup(p => p.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Permiso>>.Success([.. permisosActivos.Select(id => new Permiso { Id = id })]));
        _repo.Setup(r => r.WhereAsync(It.IsAny<Expression<Func<RolPermiso, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RolPermiso>>.Success(filasActivas));
        _repo.Setup(r => r.ObtenerActivoPorRolPermisoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RolPermiso?>.Success(null, allowNull: true));
        _repo.Setup(r => r.Add(It.IsAny<RolPermiso>())).Returns((RolPermiso e) => Result<RolPermiso>.Success(e));
        _repo.Setup(r => r.Update(It.IsAny<RolPermiso>())).Returns((RolPermiso e) => Result<RolPermiso>.Success(e));
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<RolPermiso>(It.IsAny<object>()))
            .Returns((object o) => new RolPermiso { IdRol = ((AsignarPermisoDto)o).IdRol, IdPermiso = ((AsignarPermisoDto)o).IdPermiso });
        mapper.Setup(m => m.Map<RolPermisoDto>(It.IsAny<object>()))
            .Returns((object o) => new RolPermisoDto { IdRol = ((RolPermiso)o).IdRol, IdPermiso = ((RolPermiso)o).IdPermiso });
        // S107 — el ejecutor no tiene el rol: la regla de autoedición no interviene en estas pruebas
        _accesos.Setup(a => a.AnyAsync(It.IsAny<Expression<Func<Acceso, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(false));
        return new RolPermisoService(_repo.Object, _roles.Object, _permisos.Object, _accesos.Object, mapper.Object);
    }

    private static Rol RolDelTenant(int? idTenant) => new() { Id = IdRol, Nombre = "Consultas", IdTenant = idTenant, Activo = true };

    private static CambioPermisoDto Cambio(int idPermiso, bool tiene) => new() { IdPermiso = idPermiso, Tiene = tiene };

    private void NadaEscrito()
    {
        _repo.Verify(r => r.Add(It.IsAny<RolPermiso>()), Times.Never);
        _repo.Verify(r => r.Update(It.IsAny<RolPermiso>()), Times.Never);
    }

    [Fact]
    public async Task Aplicar_agrega_y_quita_sin_commit_y_cuenta()
    {
        // el permiso 2 ya está asignado al rol; el 1 se agrega, el 2 se quita y el 3 (sin fila) ya estaba quitado
        var asignado = new RolPermiso { Id = 20, IdRol = IdRol, IdPermiso = 2, Activo = true };
        var servicio = Servicio(RolDelTenant(3), [1, 2, 3], asignado);

        var result = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true), Cambio(2, false), Cambio(3, false)], 3, Ejecutor, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Agregados);
        Assert.Equal(1, result.Value.Quitados);
        Assert.Equal(1, result.Value.SinCambio);
        _repo.Verify(r => r.Add(It.Is<RolPermiso>(e => e.IdRol == IdRol && e.IdPermiso == 1 && e.Activo && e.IdUsrMod == Ejecutor)), Times.Once);
        _repo.Verify(r => r.Update(It.Is<RolPermiso>(e => e.Id == 20 && !e.Activo && e.IdUsrMod == Ejecutor)), Times.Once);
        _repo.Verify(r => r.Add(It.IsAny<RolPermiso>()), Times.Once);
        _repo.Verify(r => r.Update(It.IsAny<RolPermiso>()), Times.Once);
        // el servicio no recibe la unidad de trabajo: no puede confirmar nada, el commit es del controlador
        Assert.DoesNotContain(typeof(RolPermisoService).GetConstructors().Single().GetParameters(),
            p => p.ParameterType.Name.Contains("UnitOfWork", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Aplicar_ya_asignado_cuenta_SinCambio()
    {
        var asignado = new RolPermiso { Id = 20, IdRol = IdRol, IdPermiso = 1, Activo = true };
        var servicio = Servicio(RolDelTenant(3), [1, 2], asignado);

        // marcar lo que ya tiene y desmarcar lo que no tiene: no hay nada que escribir
        var result = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true), Cambio(2, false)], null, Ejecutor, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Agregados);
        Assert.Equal(0, result.Value.Quitados);
        Assert.Equal(2, result.Value.SinCambio);
        NadaEscrito();
    }

    [Fact]
    public async Task Aplicar_rol_de_otro_tenant_devuelve_NOT_FOUND()
    {
        var servicio = Servicio(RolDelTenant(5), [1]);

        var result = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true)], 3, Ejecutor, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        NadaEscrito();
    }

    [Fact]
    public async Task Aplicar_rol_global_con_alcance_devuelve_NOT_FOUND()
    {
        var servicio = Servicio(RolDelTenant(null), [1]);

        var conAlcance = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true)], 3, Ejecutor, TestContext.Current.CancellationToken);
        Assert.True(conAlcance.IsFailure);
        Assert.Equal(ErrorType.NotFound, conAlcance.Error!.Type);
        NadaEscrito();

        // sin alcance (usuario de sistema) el rol global sí se puede cambiar
        var sistema = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true)], null, Ejecutor, TestContext.Current.CancellationToken);
        Assert.True(sistema.IsSuccess);
        Assert.Equal(1, sistema.Value.Agregados);
    }

    [Fact]
    public async Task Aplicar_permiso_inactivo_devuelve_PERMISO_INVALIDO()
    {
        // el 9 no está entre los permisos activos: ni siquiera se aplica el 1
        var servicio = Servicio(RolDelTenant(3), [1]);

        var result = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true), Cambio(9, true)], 3, Ejecutor, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("PERMISO_INVALIDO", result.Error!.Code);
        NadaEscrito();
    }

    [Fact]
    public async Task Aplicar_cambios_vacios_o_repetidos_devuelve_CAMBIOS_INVALIDOS()
    {
        var servicio = Servicio(RolDelTenant(3), [1, 2]);
        var ct = TestContext.Current.CancellationToken;

        var vacio = await servicio.AplicarPermisosAsync(IdRol, [], 3, Ejecutor, ct);
        var repetido = await servicio.AplicarPermisosAsync(IdRol, [Cambio(1, true), Cambio(1, false)], 3, Ejecutor, ct);
        var demasiados = await servicio.AplicarPermisosAsync(IdRol, [.. Enumerable.Range(1, 501).Select(i => Cambio(i, true))], 3, Ejecutor, ct);

        Assert.All(new[] { vacio, repetido, demasiados }, r =>
        {
            Assert.True(r.IsFailure);
            Assert.Equal("CAMBIOS_INVALIDOS", r.Error!.Code);
        });
        NadaEscrito();
    }

    [Fact]
    public async Task Asignar_fuera_del_alcance_devuelve_NOT_FOUND()
    {
        var servicio = Servicio(RolDelTenant(5), [1]);
        var ct = TestContext.Current.CancellationToken;

        var asignar = await servicio.AsignarPermisoAsync(new AsignarPermisoDto { IdRol = IdRol, IdPermiso = 1 }, 3, Ejecutor, ct);
        var desasignar = await servicio.DesasignarPermisoAsync(IdRol, 1, 3, Ejecutor, ct);

        Assert.True(asignar.IsFailure);
        Assert.Equal(ErrorType.NotFound, asignar.Error!.Type);
        Assert.True(desasignar.IsFailure);
        Assert.Equal(ErrorType.NotFound, desasignar.Error!.Type);
        NadaEscrito();
        // dentro de su tenant la asignación sigue funcionando
        _repo.Invocations.Clear();
        var propio = await Servicio(RolDelTenant(3), [1]).AsignarPermisoAsync(new AsignarPermisoDto { IdRol = IdRol, IdPermiso = 1 }, 3, Ejecutor, ct);
        Assert.True(propio.IsSuccess);
        _repo.Verify(r => r.Add(It.IsAny<RolPermiso>()), Times.Once);
    }
}
