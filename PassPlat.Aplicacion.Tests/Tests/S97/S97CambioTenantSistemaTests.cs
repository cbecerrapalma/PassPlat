using CBP.Results;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Tests.Tests.S97;

// S97 — un usuario de sistema lista todos los tenants activos y entra a cualquiera sin membresía;
// los demás siguen limitados a sus membresías activas.
public class S97CambioTenantSistemaTests
{
    private const int IdUsuario = 1;
    private const int IdTenant = 2;
    private const int IdApp = 1;

    private readonly Mock<IAuthRepository> _authRepo = new();
    private readonly Mock<IAuthenticationTokenService> _tokens = new();
    private readonly Mock<IUsuarioTenantRepository> _uts = new();
    private readonly Mock<IAppService> _apps = new();
    private readonly Mock<ITenantService> _tenants = new();

    private AuthService Crear() => new(
        _authRepo.Object, null!, null!, null!, null!, null!, null!, null!, null!, null!,
        null!, null!, null!, null!, null!, _tokens.Object, _uts.Object, null!, null!,
        _apps.Object, _tenants.Object, null!, null!);

    private void ConUsuario(bool esSistema) =>
        _authRepo.Setup(r => r.ObtenerUsuarioBasicoAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(
                new Usuario { Id = IdUsuario, NomUsuario = "u", EsSistema = esSistema }, allowNull: true));

    private void ConAppYTenant()
    {
        _apps.Setup(a => a.ObtenerPorIdAsync(IdApp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppDto?>.Success(
                new AppDto { Id = IdApp, Codigo = "PASSPLAT", Nombre = "PassPlat", Activa = true }, allowNull: true));
        _tenants.Setup(t => t.ObtenerPorIdAsync(IdTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TenantDto?>.Success(
                new TenantDto { Id = IdTenant, Codigo = "ABARROTES", Nombre = "Abarrotes del Sur", Activo = true }, allowNull: true));
    }

    private void ConMembresia(UsuarioTenant? membresia) =>
        _uts.Setup(u => u.ObtenerActivoPorTenantAsync(IdUsuario, IdTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(membresia, allowNull: true));

    private void ConExisteMembresia(bool existe) =>
        _uts.Setup(u => u.ExisteMembresiaAsync(IdUsuario, IdTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(existe));

    private void ConToken() =>
        _tokens.Setup(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthenticationResult>.Success(
                new AuthenticationResult("acc", "ref", "jti", DateTime.Now.AddHours(1), null, AuthenticationOrigin.SwitchTenant)));

    private Task<Result<PassPlat.Aplicacion.Dtos.Core.AuthResponseDto>> Cambiar() =>
        Crear().SwitchTenantAsync(IdUsuario, IdTenant, IdApp, null, null, null, TestContext.Current.CancellationToken);

    [Fact]
    public async Task MisTenants_UsuarioSistema_DevuelveTodosLosActivos()
    {
        ConUsuario(esSistema: true);
        IReadOnlyList<TenantDto> activos =
        [
            new TenantDto { Id = 1, Codigo = "PLATFORM", Nombre = "Plataforma", Activo = true },
            new TenantDto { Id = 2, Codigo = "ABARROTES", Nombre = "Abarrotes del Sur", Activo = true },
            new TenantDto { Id = 4, Codigo = "E2E_TEST", Nombre = "Inventa E2E Test", Activo = true }
        ];
        _tenants.Setup(t => t.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TenantDto>>.Success(activos));

        var r = await Crear().ObtenerMisTenantsAsync(IdUsuario, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Equal(new[] { 1, 2, 4 }, r.Value!.Select(t => t.Id).ToArray());
        _uts.Verify(u => u.ObtenerActivosPorUsuarioAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MisTenants_UsuarioNormal_DevuelveSusMembresias()
    {
        ConUsuario(esSistema: false);
        IReadOnlyList<UsuarioTenant> membresias =
        [
            new UsuarioTenant
            {
                Id = 3, IdUsuario = IdUsuario, IdTenant = IdTenant,
                Tenant = new Tenant { Id = IdTenant, Codigo = "ABARROTES", Nombre = "Abarrotes del Sur", Activo = true }
            }
        ];
        _uts.Setup(u => u.ObtenerActivosPorUsuarioAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<UsuarioTenant>>.Success(membresias));

        var r = await Crear().ObtenerMisTenantsAsync(IdUsuario, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        var tenant = Assert.Single(r.Value!);
        Assert.Equal(IdTenant, tenant.Id);
        Assert.Equal("Abarrotes del Sur", tenant.Nombre);
        _tenants.Verify(t => t.ObtenerActivosAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchTenant_SistemaSinMembresia_EmiteTokenSinUsuarioTenant()
    {
        ConAppYTenant();
        ConMembresia(null);
        ConExisteMembresia(false);
        ConUsuario(esSistema: true);
        ConToken();

        var r = await Cambiar();

        Assert.True(r.IsSuccess);
        Assert.Equal(IdTenant, r.Value!.IdTenant);
        _tokens.Verify(t => t.LoginAsync(
            It.Is<AuthenticationContext>(c => c.IdTenant == IdTenant && c.EsSistema && c.IdUsuarioTenant == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SwitchTenant_NoSistemaSinMembresia_RetornaSinAccesoTenant()
    {
        ConAppYTenant();
        ConMembresia(null);
        ConUsuario(esSistema: false);

        var r = await Cambiar();

        Assert.True(r.IsFailure);
        Assert.Equal("SIN_ACCESO_TENANT", r.Error!.Code);
        _tokens.Verify(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchTenant_SistemaConMembresiaInactiva_RetornaSinAccesoTenant()
    {
        ConAppYTenant();
        // El repositorio filtra Activo: una membresía inactiva llega como null y solo ExisteMembresiaAsync la revela
        ConMembresia(null);
        ConExisteMembresia(true);
        ConUsuario(esSistema: true);

        var r = await Cambiar();

        Assert.True(r.IsFailure);
        Assert.Equal("SIN_ACCESO_TENANT", r.Error!.Code);
        _tokens.Verify(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchTenant_ConMembresia_EmiteTokenConUsuarioTenant()
    {
        ConAppYTenant();
        ConMembresia(new UsuarioTenant { Id = 3, IdUsuario = IdUsuario, IdTenant = IdTenant, IdEstado = (int)EEstadoUsuario.Activo, Activo = true });
        ConUsuario(esSistema: false);
        ConToken();

        var r = await Cambiar();

        Assert.True(r.IsSuccess);
        _tokens.Verify(t => t.LoginAsync(
            It.Is<AuthenticationContext>(c => c.IdUsuarioTenant == 3 && !c.EsSistema),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
