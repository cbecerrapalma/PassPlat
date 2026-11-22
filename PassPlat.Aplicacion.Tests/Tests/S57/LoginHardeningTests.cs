using System.IdentityModel.Tokens.Jwt;
using CBP.Authentication.JwtBearer;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Tests.Tests.S57;

// S57.19 — Hardening de login: H-1 (IdApp/IdTenant + Apps.Activa + tenant activo en todos
// los métodos de login) y H-2 (audiencia JWT por Apps.Codigo, nunca hardcode numérico).
public class LoginHardeningTests
{
    private static AuthService CrearAuthService(
        Mock<IAppService> apps,
        Mock<ITenantService> tenants,
        Mock<IUsuarioTenantRepository>? uts = null)
    {
        return new AuthService(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, uts?.Object ?? null!, null!, null!,
            apps.Object, tenants.Object, null!, null!);
    }

    private static void SetupAppTenant(
        Mock<IAppService> apps, Mock<ITenantService> tenants,
        bool appActiva = true, bool tenantActivo = true,
        string appCodigo = "INVENTA", int appId = 12, int tenantId = 4)
    {
        apps.Setup(a => a.ObtenerPorIdAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppDto?>.Success(
                new AppDto { Id = appId, Codigo = appCodigo, Nombre = appCodigo, Activa = appActiva },
                allowNull: true));
        tenants.Setup(t => t.ObtenerPorIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TenantDto?>.Success(
                new TenantDto { Id = tenantId, Codigo = "T", Nombre = "T", Activo = tenantActivo },
                allowNull: true));
    }

    [Fact]
    public async Task LoginConToken_IdAppCero_RetornaLoginValidation()
    {
        var svc = CrearAuthService(new Mock<IAppService>(), new Mock<ITenantService>());
        var r = await svc.LoginConTokenAsync("u", null, 0, "h", 4, null, null, null, TestContext.Current.CancellationToken);
        Assert.True(r.IsFailure);
        Assert.Equal("LOGIN_VALIDATION", r.Error!.Code);
    }

    [Fact]
    public async Task LoginConToken_AppInexistente_RetornaLoginValidation()
    {
        var apps = new Mock<IAppService>();
        var tenants = new Mock<ITenantService>();
        apps.Setup(a => a.ObtenerPorIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppDto?>.Success(null, allowNull: true));
        var svc = CrearAuthService(apps, tenants);
        var r = await svc.LoginConTokenAsync("u", null, 99, "h", 4, null, null, null, TestContext.Current.CancellationToken);
        Assert.True(r.IsFailure);
        Assert.Equal("LOGIN_VALIDATION", r.Error!.Code);
        Assert.Contains("APP_INEXISTENTE", r.Error.Message);
    }

    [Fact]
    public async Task LoginConToken_AppInactiva_RetornaLoginValidation()
    {
        var apps = new Mock<IAppService>();
        var tenants = new Mock<ITenantService>();
        SetupAppTenant(apps, tenants, appActiva: false, appCodigo: "APP_CI_232107", appId: 2, tenantId: 4);
        var svc = CrearAuthService(apps, tenants);
        var r = await svc.LoginConTokenAsync("u", null, 2, "h", 4, null, null, null, TestContext.Current.CancellationToken);
        Assert.True(r.IsFailure);
        Assert.Equal("LOGIN_VALIDATION", r.Error!.Code);
        Assert.Contains("APP_INACTIVA", r.Error.Message);
    }

    [Fact]
    public async Task LoginConToken_TenantInexistente_RetornaLoginValidation()
    {
        var apps = new Mock<IAppService>();
        var tenants = new Mock<ITenantService>();
        SetupAppTenant(apps, tenants);
        tenants.Setup(t => t.ObtenerPorIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TenantDto?>.Success(null, allowNull: true));
        var svc = CrearAuthService(apps, tenants);
        var r = await svc.LoginConTokenAsync("u", null, 12, "h", 999, null, null, null, TestContext.Current.CancellationToken);
        Assert.True(r.IsFailure);
        Assert.Equal("LOGIN_VALIDATION", r.Error!.Code);
        Assert.Contains("TENANT_INEXISTENTE", r.Error.Message);
    }

    [Fact]
    public async Task SwitchTenant_GuardOk_SinMembresia_RetornaSinAccesoTenant()
    {
        var apps = new Mock<IAppService>();
        var tenants = new Mock<ITenantService>();
        SetupAppTenant(apps, tenants);
        var uts = new Mock<IUsuarioTenantRepository>();
        uts.Setup(u => u.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Dominio.Entities.Core.UsuarioTenant?>.Failure("X", "Y"));
        var svc = CrearAuthService(apps, tenants, uts);
        var r = await svc.SwitchTenantAsync(8, 4, 12, null, null, null, TestContext.Current.CancellationToken);
        Assert.True(r.IsFailure);
        // El guard H-1 pasó (app+tenant válidos); falla por membresía, no por validación.
        Assert.Equal("SIN_ACCESO_TENANT", r.Error!.Code);
    }

    private static AuthenticationTokenIssuer CrearIssuer()
    {
        var options = new JwtOptions
        {
            SecretKey = "S5719-test-secret-key-32-chars-min!",
            Issuer = "PassPlat",
            Audience = "PassPlat",
            ExpirationMinutes = 60,
            RefreshTokenExpirationMinutes = 1440
        };
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh-test");
        // Rama no-Inventa: delega en IJwtTokenService (usa JwtOptions.Audience como el servicio real).
        jwt.Setup(j => j.GenerateToken(It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
            .Returns((IEnumerable<System.Security.Claims.Claim> claims) =>
            {
                var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(options.SecretKey));
                var token = new JwtSecurityToken(
                    issuer: options.Issuer, audience: options.Audience, claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(60),
                    signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                        key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
                return new JwtSecurityTokenHandler().WriteToken(token);
            });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AudienceInventa"] = "InventaNet"
            })
            .Build();
        return new AuthenticationTokenIssuer(
            jwt.Object, options, Mock.Of<ILoggerService>(), Mock.Of<IHttpContextAccessor>(), config);
    }

    private static string LeerAudiencia(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return Assert.Single(jwt.Audiences);
    }

    [Fact]
    public void Issuer_AppCodigoInventa_EmiteAudienciaInventaNet()
    {
        var issuer = CrearIssuer();
        var ctx = new AuthenticationContext(8, 4, 12, null, null,
            AuthenticationOrigin.Login, EsSistema: false, IdUsuarioTenant: 8, AppCodigo: "INVENTA");
        var r = issuer.Generate(ctx, Array.Empty<System.Security.Claims.Claim>());
        Assert.Equal("InventaNet", LeerAudiencia(r.AccessToken));
    }

    [Fact]
    public void Issuer_AppCodigoStore_EmiteAudienciaInventaNet()
    {
        var issuer = CrearIssuer();
        var ctx = new AuthenticationContext(8, 4, 13, null, null,
            AuthenticationOrigin.Login, EsSistema: false, IdUsuarioTenant: 8, AppCodigo: "INVENTA_STORE");
        var r = issuer.Generate(ctx, Array.Empty<System.Security.Claims.Claim>());
        Assert.Equal("InventaNet", LeerAudiencia(r.AccessToken));
    }

    [Fact]
    public void Issuer_AppCodigoPassplat_MantieneAudienciaPassPlat()
    {
        var issuer = CrearIssuer();
        var ctx = new AuthenticationContext(1, 1, 1, null, null,
            AuthenticationOrigin.Login, EsSistema: true, IdUsuarioTenant: null, AppCodigo: "PASSPLAT");
        var r = issuer.Generate(ctx, Array.Empty<System.Security.Claims.Claim>());
        Assert.Equal("PassPlat", LeerAudiencia(r.AccessToken));
    }

    [Fact]
    public void Issuer_LegacySinCodigo_IdApp2_MantieneCompatInventaNet()
    {
        var issuer = CrearIssuer();
        var ctx = new AuthenticationContext(3, 2, 2, null, null,
            AuthenticationOrigin.Login, EsSistema: false, IdUsuarioTenant: null, AppCodigo: null);
        var r = issuer.Generate(ctx, Array.Empty<System.Security.Claims.Claim>());
        Assert.Equal("InventaNet", LeerAudiencia(r.AccessToken));
    }
}

