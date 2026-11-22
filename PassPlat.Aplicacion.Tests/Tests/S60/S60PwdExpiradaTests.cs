using CBP.Logging.Interfaces;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S60;

// S60 · D4/D5/D8 — el login con contraseña vencida se rechaza con PWD_EXPIRADA (403),
// sin token y sin MFA; y el aviso de vencimiento ignora las políticas SoloPorRol y a
// quien tiene vigencia efectiva 0 (FecExpira NULL).
public class S60PwdExpiradaTests
{
    private const int IdApp = 1;
    private const int IdTenant = 3;

    private static (AuthService svc, Mock<IAuthRepository> repo, Mock<IEmailQueue> email,
        Mock<IAuthenticationTokenService> tokens, Mock<IAuditoriaPwdRepository> auditoria) CrearAuthService(
        LoginResult resultadoSp)
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
                new AppDto { Id = IdApp, Codigo = "PASSPLAT", Nombre = "PassPlat", Activa = true },
                allowNull: true));
        tenants.Setup(t => t.ObtenerPorIdAsync(IdTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TenantDto?>.Success(
                new TenantDto { Id = IdTenant, Codigo = "VESTUARIO", Nombre = "V", Activo = true },
                allowNull: true));

        repo.Setup(r => r.ObtenerUsuarioPorNomAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Usuario?>.Success(new Usuario
            {
                Id = 4,
                IdTenant = IdTenant,
                NomUsuario = "admin_vestuario",
                EsSistema = false
            }));
        repo.Setup(r => r.ObtenerHashActualAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success("hash-argon2id-guardado", allowNull: true));
        repo.Setup(r => r.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LoginResult>.Success(resultadoSp));

        pwd.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        auditoria.Setup(a => a.RegistrarAuditoria(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<long?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Returns(Result<AuditoriaPwd>.Success(new AuditoriaPwd()));

        var svc = new AuthService(
            repo.Object, null!, null!, auditoria.Object, null!, pwd.Object, null!, null!, email.Object,
            null!, null!, null!, null!, null!, httpContext.Object, tokens.Object, null!, null!, olog.Object,
            apps.Object, tenants.Object, null!, null!);

        return (svc, repo, email, tokens, auditoria);
    }

    [Fact]
    public async Task LoginConToken_ContrasenaVencida_RetornaPwdExpirada_SinTokenNiMfa()
    {
        var (svc, _, email, tokens, auditoria) = CrearAuthService(new LoginResult
        {
            Resultado = LoginResult.IdPwdExpirada,
            Mensaje = "Contrasena vencida",
            IdUsuario = 4,
            IdTenant = IdTenant,
            PwdExpirada = true
        });

        var r = await svc.LoginConTokenAsync("admin_vestuario", null, IdApp, "hash", IdTenant, null, null, null, TestContext.Current.CancellationToken);

        Assert.True(r.IsFailure);
        Assert.Equal("PWD_EXPIRADA", r.Error!.Code);
        Assert.Equal(AuthService.MensajePwdExpirada, r.Error.Message);
        Assert.Contains("venció", r.Error.Message);
        Assert.Contains("¿Olvidaste tu contraseña?", r.Error.Message);

        // Sin token, sin MFA y sin correo: el rechazo es anterior a cualquier emision.
        tokens.Verify(t => t.LoginAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Never);
        tokens.Verify(t => t.OAuthAsync(It.IsAny<AuthenticationContext>(), It.IsAny<CancellationToken>()), Times.Never);
        tokens.Verify(t => t.RefreshAsync(It.IsAny<AuthenticationContext>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(e => e.EnqueueAsync(It.IsAny<EmailJob>(), It.IsAny<CancellationToken>()), Times.Never);
        auditoria.Verify(a => a.RegistrarAuditoria(4, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<long?>(),
            It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task LoginConToken_OtroResultado_FallaConLoginFailed()
    {
        var (svc, _, _, _, _) = CrearAuthService(new LoginResult
        {
            Resultado = 2, // CredencialesInvalidas
            Mensaje = "Credenciales invalidas",
            IdUsuario = 4,
            IdTenant = IdTenant
        });

        var r = await svc.LoginConTokenAsync("admin_vestuario", null, IdApp, "hash", IdTenant, null, null, null, TestContext.Current.CancellationToken);

        Assert.True(r.IsFailure);
        Assert.Equal("LOGIN_FAILED", r.Error!.Code);
    }

    // S60 · D8 — el aviso de vencimiento ignora las políticas SoloPorRol y no avisa a
    // quien tiene vigencia efectiva 0 (FecExpira NULL).
    [Fact]
    public async Task AvisoDeExpiracion_IgnoraPoliticasSoloPorRol_Y_VigenciaCero()
    {
        var politicas = new Mock<IPoliticaPwdRepository>();
        var usuarios = new Mock<IUsuarioRepository>();
        var membresias = new Mock<IUsuarioTenantRepository>();
        var historial = new Mock<IHistorialPwdRepository>();

        politicas.Setup(p => p.ObtenerPorTenantAsync(0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PoliticaPwd>>.Success(new List<PoliticaPwd>
            {
                new() { Id = 1, IdTenant = null, Codigo = "DEFAULT", Activa = true, DiasVigencia = 90, SoloPorRol = false },
                new() { Id = 2, IdTenant = null, Codigo = "SIN_VENCIMIENTO", Activa = true, DiasVigencia = 0, SoloPorRol = true }
            }));

        usuarios.Setup(u => u.GetAllAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Usuario>>.Success(new List<Usuario>
            {
                new() { Id = 4, IdTenant = IdTenant, NomUsuario = "admin_vestuario", Email = "a@b.c", EmailVerificado = true }
            }));

        membresias.Setup(m => m.ObtenerActivosPorUsuarioAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<UsuarioTenant>>.Success(new List<UsuarioTenant>
            {
                new() { IdUsuario = 4, IdTenant = IdTenant, IdEstado = 1, Activo = true }
            }));

        // Vigencia efectiva 0: sin FecExpira no hay vencimiento que avisar.
        var ahora = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Local);
        historial.Setup(h => h.ObtenerHistorialRecienteAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<HistorialPwd>>.Success(new List<HistorialPwd>
            {
                new() { IdUsuario = 4, EsActual = true, FecRegistro = ahora.AddDays(-10), FecExpira = null }
            }));

        var resultado = await InvocarSeleccionAsync(politicas.Object, usuarios.Object, membresias.Object, historial.Object, ahora);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task AvisoDeExpiracion_AvisaCuandoFaltaPocoParaVencer()
    {
        var politicas = new Mock<IPoliticaPwdRepository>();
        var usuarios = new Mock<IUsuarioRepository>();
        var membresias = new Mock<IUsuarioTenantRepository>();
        var historial = new Mock<IHistorialPwdRepository>();

        politicas.Setup(p => p.ObtenerPorTenantAsync(0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PoliticaPwd>>.Success(new List<PoliticaPwd>
            {
                new() { Id = 1, IdTenant = null, Codigo = "DEFAULT", Activa = true, DiasVigencia = 90, SoloPorRol = false }
            }));

        usuarios.Setup(u => u.GetAllAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Usuario>>.Success(new List<Usuario>
            {
                new() { Id = 4, IdTenant = IdTenant, NomUsuario = "admin_vestuario", Email = "a@b.c", EmailVerificado = true }
            }));

        membresias.Setup(m => m.ObtenerActivosPorUsuarioAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<UsuarioTenant>>.Success(new List<UsuarioTenant>
            {
                new() { IdUsuario = 4, IdTenant = IdTenant, IdEstado = 1, Activo = true }
            }));

        // La misma fecha de referencia para el historico y para el calculo del servicio.
        var ahora = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Local);
        historial.Setup(h => h.ObtenerHistorialRecienteAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<HistorialPwd>>.Success(new List<HistorialPwd>
            {
                new() { IdUsuario = 4, EsActual = true, FecRegistro = ahora.AddDays(-10), FecExpira = ahora.AddDays(7) }
            }));

        var resultado = await InvocarSeleccionAsync(politicas.Object, usuarios.Object, membresias.Object, historial.Object, ahora);

        Assert.Single(resultado);
        Assert.Equal(4, resultado[0].Usuario.Id);
    }

    private static async Task<List<(UsuarioDto Usuario, int DiasRestantes, DateTime FechaExpira)>> InvocarSeleccionAsync(
        IPoliticaPwdRepository politicas,
        IUsuarioRepository usuarios,
        IUsuarioTenantRepository membresias,
        IHistorialPwdRepository historial,
        DateTime currentDate)
    {
        var metodo = typeof(PasswordExpirationBackgroundService).GetMethod(
            "ObtenerUsuariosConExpiracionAsyncConManejoErrores",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(metodo);

        var tarea = (Task)metodo!.Invoke(null, new object?[]
        {
            historial, politicas, usuarios, membresias, currentDate, CancellationToken.None
        })!;
        await tarea;

        var resultado = (System.Collections.IEnumerable)tarea.GetType().GetProperty("Result")!.GetValue(tarea)!;
        var lista = new List<(UsuarioDto, int, DateTime)>();
        foreach (var item in resultado)
        {
            var tipo = item!.GetType();
            var tupla = (UsuarioDto)tipo.GetField("Item1")!.GetValue(item)!;
            var dias = (int)tipo.GetField("Item2")!.GetValue(item)!;
            var fecha = (DateTime)tipo.GetField("Item3")!.GetValue(item)!;
            lista.Add((tupla, dias, fecha));
        }
        return lista;
    }
}
