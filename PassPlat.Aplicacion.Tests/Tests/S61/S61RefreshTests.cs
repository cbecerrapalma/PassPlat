using System.Security.Cryptography;
using System.Text;
using CBP.Authentication.JwtBearer;
using CBP.Data.Abstractions;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S61;

/// <summary>
/// S61 - K4 (D4): una sesion de plataforma (IdTenant 0) solo renueva si el usuario sigue
/// cumpliendo D1; si no, la sesion se revoca y no se emiten tokens.
/// </summary>
public class S61RefreshTests
{
    private const int IdUsuario = 9;
    private const int IdApp = 12;
    private const string RefreshToken = "refresh-s61-plataforma";

    private static string Hash(string valor)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    private static (AuthService sut, PassPlatDbContext ctx) Build(bool cumpleD1)
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new PassPlatDbContext(options);

        ctx.Sesiones.Add(new Sesion
        {
            Id = Guid.NewGuid(),
            IdUsuario = IdUsuario,
            IdTenant = 0,
            IdApp = IdApp,
            IdTokenExt = "ext",
            HashRefresh = Hash(RefreshToken),
            FecInicio = DateTime.Now.AddMinutes(-5),
            UltActividad = DateTime.Now.AddMinutes(-1),
            FecExpira = DateTime.Now.AddHours(1),
            EsActiva = true
        });
        ctx.SaveChanges();

        var sesionRepo = new SesionRepository(ctx, new FakeUnitOfWork(ctx), NullLogger<SesionRepository>.Instance);

        var authRepo = new Mock<IAuthRepository>();
        authRepo.Setup(r => r.ExisteAccesoPlatformActivoAsync(IdUsuario, IdApp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(cumpleD1));
        authRepo.Setup(r => r.ObtenerUsuarioBasicoAsync(IdUsuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(
                new Usuario { Id = IdUsuario, NomUsuario = "admin", Email = "admin@test.local", EsSistema = true },
                allowNull: true));

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(IdUsuario, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(null, allowNull: true));

        usuarioTenantRepo
            .Setup(r => r.ResolverIdUsuarioTenantAsync(IdUsuario, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));

        var tokenService = new Mock<IAuthenticationTokenService>();
        tokenService.Setup(t => t.RefreshAsync(It.IsAny<AuthenticationContext>(), It.IsAny<string?>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthenticationResult>.Success(new AuthenticationResult(
                "access-token", "refresh-token", "jti", DateTime.UtcNow.AddHours(1), null, AuthenticationOrigin.Refresh)));


        var appService = new Mock<IAppService>();
        appService.Setup(s => s.ObtenerPorIdAsync(IdApp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppDto?>.Success(
                new AppDto { Id = IdApp, Codigo = "INVENTA", Nombre = "Inventa POS", Activa = true },
                allowNull: true));

        var sut = new AuthService(
            authRepo.Object,
            sesionRepo,
            Mock.Of<IIntentoAccesoService>(),
            Mock.Of<IAuditoriaPwdRepository>(),
            Mock.Of<IJwtTokenService>(),
            Mock.Of<CBP.Security.Cryptography.Services.IPasswordService>(),
            new JwtOptions(),
            NullLogger<AuthService>.Instance,
            Mock.Of<IEmailQueue>(),
            Mock.Of<IMFARepository>(),
            Mock.Of<IMfaCodeStore>(),
            Microsoft.Extensions.Options.Options.Create(new MfaOptions()),
            Mock.Of<IIPService>(),
            Mock.Of<IDispConfiableService>(),
            Mock.Of<IHttpContextAccessor>(),
            tokenService.Object,
            usuarioTenantRepo.Object,
            null!,
            Mock.Of<ILoggerService>(),
            appService.Object,
            Mock.Of<ITenantService>(),
            Mock.Of<IAccesoRepository>(),
            new FakeUnitOfWork(ctx));

        return (sut, ctx);
    }

    [Fact]
    public async Task Renovar_sin_cumplir_D1_devuelve_SIN_ACCESO_PLATFORM_y_revoca_la_sesion()
    {
        var (sut, ctx) = Build(cumpleD1: false);

        var result = await sut.RefreshTokenAsync(RefreshToken, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("SIN_ACCESO_PLATFORM", result.Error!.Code);
        Assert.False(ctx.Sesiones.AsNoTracking().Single().EsActiva);
    }

    [Fact]
    public async Task Renovar_cumpliendo_D1_no_falla_por_alcance()
    {
        var (sut, _) = Build(cumpleD1: true);

        var result = await sut.RefreshTokenAsync(RefreshToken, TestContext.Current.CancellationToken);

        Assert.False(result.IsFailure && result.Error!.Code == "SIN_ACCESO_PLATFORM");
    }
}
