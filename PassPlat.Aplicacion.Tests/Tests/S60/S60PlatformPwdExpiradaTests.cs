using CBP.Logging.Interfaces;
using CBP.Results;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S60;

// S60 · D10 (adenda 3.1) — login/platform también respeta el vencimiento de la clave:
// con la clave correcta y la contraseña vencida devuelve PWD_EXPIRADA 403, sin token y
// sin llegar a comprobar el acceso de plataforma; vigente sigue igual que antes.
public class S60PlatformPwdExpiradaTests
{
    private const int IdApp = 12;
    private const int IdTenant = 3;
    private const int IdUsuario = 4;

    private static (AuthService svc, Mock<IAuthRepository> repo, Mock<IAuthenticationTokenService> tokens)
        CrearAuthService(bool pwdExpirada, bool accesoPlatform)
    {
        var repo = new Mock<IAuthRepository>();
        var pwd = new Mock<CBP.Security.Cryptography.Services.IPasswordService>();
        var email = new Mock<IEmailQueue>();
        var tokens = new Mock<IAuthenticationTokenService>();
        var auditoria = new Mock<IAuditoriaPwdRepository>();
        var olog = new Mock<ILoggerService>();
        var httpContext = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        httpContext.Setup(h => h.HttpContext).Returns((Microsoft.AspNetCore.Http.HttpContext?)null);
        var apps = new Mock<IAppService>();
        var tenants = new Mock<ITenantService>();

        apps.Setup(a => a.ObtenerPorIdAsync(IdApp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppDto?>.Success(
                new AppDto { Id = IdApp, Codigo = "INVENTA", Nombre = "Inventa", Activa = true },
                allowNull: true));

        repo.Setup(r => r.ObtenerUsuarioPorNomAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(new Usuario
            {
                Id = IdUsuario,
                IdTenant = IdTenant,
                IdEstado = (int)PassPlat.Dominio.Enums.EEstadoUsuario.Activo,
                NomUsuario = "admin_vestuario",
                EsSistema = false
            }));

        repo.Setup(r => r.ObtenerHashActualAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success("hash-argon2id-guardado", allowNull: true));

        repo.Setup(r => r.ObtenerPwdExpiradaAsync(IdUsuario, IdTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(pwdExpirada));

        repo.Setup(r => r.ExisteAccesoPlatformActivoAsync(IdUsuario, IdApp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(accesoPlatform));

        pwd.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        tokens.Setup(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthenticationResult>.Success(new AuthenticationResult(
                "access-token", "refresh-token", "jti", DateTime.UtcNow.AddHours(1), null, AuthenticationOrigin.Login)));

        var svc = new AuthService(
            repo.Object, null!, null!, auditoria.Object, null!, pwd.Object, null!, null!, email.Object,
            null!, null!, null!, null!, null!, httpContext.Object, tokens.Object, null!, null!, olog.Object,
            apps.Object, tenants.Object, null!, null!);

        return (svc, repo, tokens);
    }

    [Fact]
    public async Task PlatformLogin_ContrasenaVencida_RetornaPwdExpirada_SinTokenNiAcceso()
    {
        var (svc, repo, tokens) = CrearAuthService(pwdExpirada: true, accesoPlatform: true);

        var r = await svc.PlatformLoginAsync("admin_vestuario", "clave", IdApp, null, null, null,
            TestContext.Current.CancellationToken);

        Assert.True(r.IsFailure);
        Assert.Equal("PWD_EXPIRADA", r.Error!.Code);
        Assert.Equal(AuthService.MensajePwdExpirada, r.Error.Message);

        // El rechazo es anterior al control de acceso y a la emisión del token.
        repo.Verify(rp => rp.ExisteAccesoPlatformActivoAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        tokens.Verify(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlatformLogin_ContrasenaVigente_EmiteTokenComoAntes()
    {
        var (svc, repo, tokens) = CrearAuthService(pwdExpirada: false, accesoPlatform: true);

        var r = await svc.PlatformLoginAsync("admin_vestuario", "clave", IdApp, null, null, null,
            TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Equal("access-token", r.Value!.AccessToken);
        repo.Verify(rp => rp.ObtenerPwdExpiradaAsync(IdUsuario, IdTenant, It.IsAny<CancellationToken>()), Times.Once);
        tokens.Verify(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
