using System.Linq.Expressions;
using System.Text.RegularExpressions;
using CBP.Caching.Interfaces;
using CBP.Data.Abstractions;
using CBP.Results;
using Moq;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Contexto;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests;

/// <summary>
/// S78 — DashboardEnterpriseService cuenta y agrupa en la BD (CountAsync/CountByAsync) con el filtro de tenant
/// dentro del predicado, propaga los fallos de repositorio y solo usa GetAllAsync en catálogos o con límite.
/// </summary>
public class S78DashboardEnterpriseServiceTests
{
    private static Mock<TRepo> Repo<TRepo, TEntity>()
        where TRepo : class, IRepositoryAsync<TEntity>
        where TEntity : class
    {
        var m = new Mock<TRepo>();
        m.Setup(r => r.Query()).Returns(Array.Empty<TEntity>().AsQueryable());
        m.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(0));
        m.Setup(r => r.CountAsync(It.IsAny<Expression<Func<TEntity, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(0));
        m.Setup(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<IReadOnlyList<TEntity>>.Success(Array.Empty<TEntity>()));
        m.Setup(r => r.GetAllAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<IReadOnlyList<TEntity>>.Success(Array.Empty<TEntity>()));
        return m;
    }

    private sealed class Fixture
    {
        public readonly Mock<IUsuarioRepository> Usuarios = Repo<IUsuarioRepository, Usuario>();
        public readonly Mock<ITenantRepository> Tenants = Repo<ITenantRepository, Tenant>();
        public readonly Mock<ISesionRepository> Sesiones = Repo<ISesionRepository, Sesion>();
        public readonly Mock<IDispConfiableRepository> DispConf = Repo<IDispConfiableRepository, DispConfiable>();
        public readonly Mock<IIdenExtRepository> IdenExt = Repo<IIdenExtRepository, IdenExt>();
        public readonly Mock<IEmailLogRepository> EmailLog = Repo<IEmailLogRepository, EmailLog>();
        public readonly Mock<IIntentoAccesoRepository> Intentos = Repo<IIntentoAccesoRepository, IntentoAcceso>();
        public readonly Mock<IBloqueoRepository> Bloqueos = Repo<IBloqueoRepository, Bloqueo>();
        public readonly Mock<IMFARepository> Mfa = Repo<IMFARepository, MFA>();
        public readonly Mock<IHistorialPwdRepository> HistPwd = Repo<IHistorialPwdRepository, HistorialPwd>();
        public readonly Mock<IAuditoriaPwdRepository> Auditoria = Repo<IAuditoriaPwdRepository, AuditoriaPwd>();
        public readonly Mock<IProvIdenRepository> ProvIden = Repo<IProvIdenRepository, ProvIden>();
        public readonly Mock<IAudIdenExtRepository> AudIdenExt = Repo<IAudIdenExtRepository, AudIdenExt>();
        public readonly Mock<IDispRepository> Disp = Repo<IDispRepository, Disp>();
        public readonly Mock<IIPRepository> Ips = Repo<IIPRepository, IP>();
        public readonly Mock<IEmailTemplateRepository> Templates = Repo<IEmailTemplateRepository, EmailTemplate>();
        public readonly Mock<IHistorialIdenExtRepository> HistIden = Repo<IHistorialIdenExtRepository, HistorialIdenExt>();
        public readonly Mock<IAppRepository> Apps = Repo<IAppRepository, App>();
        public readonly Mock<IRolRepository> Roles = Repo<IRolRepository, Rol>();
        public readonly Mock<IPermisoRepository> Permisos = Repo<IPermisoRepository, Permiso>();
        public readonly Mock<INotificacionRepository> Notificaciones = Repo<INotificacionRepository, Notificacion>();
        public readonly Mock<IIdenExtTokensRepository> Tokens = Repo<IIdenExtTokensRepository, IdenExtTokens>();
        public readonly Mock<IUsuarioTenantRepository> UsuarioTenant = Repo<IUsuarioTenantRepository, UsuarioTenant>();

        public DashboardEnterpriseService Crear() => new(
            Usuarios.Object, Tenants.Object, Sesiones.Object, DispConf.Object, IdenExt.Object, EmailLog.Object,
            Intentos.Object, Bloqueos.Object, Mfa.Object, HistPwd.Object, Auditoria.Object, ProvIden.Object,
            AudIdenExt.Object, Disp.Object, Ips.Object, Templates.Object, HistIden.Object, Apps.Object,
            Roles.Object, Permisos.Object, Notificaciones.Object, Tokens.Object, UsuarioTenant.Object,
            Mock.Of<IBackgroundStatusService>(), Mock.Of<ICacheService>());
    }

    [Fact]
    public async Task GetEjecutivo_con_tenant_cuenta_solo_miembros()
    {
        var fx = new Fixture();
        fx.UsuarioTenant.Setup(r => r.ObtenerIdsUsuariosActivosPorTenantAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<int>>.Success([5]));
        var predicados = new List<Expression<Func<Usuario, bool>>>();
        fx.Usuarios.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Usuario, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Usuario, bool>>, CancellationToken>((p, _) => predicados.Add(p))
            .ReturnsAsync(Result<int>.Success(1));

        var result = await fx.Crear().GetEjecutivoAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalUsuarios);
        var miembro = new Usuario { Id = 5, IdEstado = 1, NomUsuario = "u5", Nombre = "U", Apellido = "Cinco" };
        var ajeno = new Usuario { Id = 7, IdEstado = 1, NomUsuario = "u7", Nombre = "U", Apellido = "Siete" };
        Assert.NotEmpty(predicados);
        Assert.True(predicados[0].Compile()(miembro));
        Assert.All(predicados, p => Assert.False(p.Compile()(ajeno)));
    }

    [Fact]
    public async Task GetEjecutivo_no_llama_GetAllAsync_de_usuarios()
    {
        var fx = new Fixture();

        var result = await fx.Crear().GetEjecutivoAsync(null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        fx.Usuarios.Verify(r => r.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        fx.Usuarios.Verify(r => r.GetAllAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        fx.Usuarios.Verify(r => r.CountAsync(It.IsAny<Expression<Func<Usuario, bool>>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetSeguridad_falla_de_repositorio_propaga_Failure()
    {
        var fx = new Fixture();
        fx.Intentos.Setup(r => r.CountAsync(It.IsAny<Expression<Func<IntentoAcceso, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("DATABASE_ERROR", "sin conexión"));

        var result = await fx.Crear().GetSeguridadAsync(null, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("DATABASE_ERROR", result.Error!.Code);
    }

    [Fact]
    public void Servicio_solo_usa_GetAllAsync_en_catalogos_o_con_limite()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var fuente = File.ReadAllText(Path.Combine(dir!.FullName,
            "PassPlat.Aplicacion", "Services", "Dashboard", "DashboardEnterpriseService.cs"));

        var usos = Regex.Matches(fuente, @"(\w+)\.GetAllAsync\(([^,)]*)");
        Assert.NotEmpty(usos);
        Assert.All(usos, m => Assert.True(
            m.Groups[1].Value is "_provIdenRepo" or "_templateRepo" || int.TryParse(m.Groups[2].Value.Trim(), out _),
            $"GetAllAsync sin límite fuera de catálogo: {m.Value}"));
        Assert.DoesNotContain("ObtenerTodosConDispositivoAsync", fuente);
    }
}
