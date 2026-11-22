using CBP.Caching.Interfaces;
using CBP.Data.Abstractions;
using CBP.Logging.Interfaces;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S77;

/// <summary>
/// S77 — inicio por perfil: cuenta (siempre), tenant (con tenant en el token) y
/// plataforma (sin tenant, totales globales). Un campo null = el actor no tiene el permiso.
/// </summary>
public class S77InicioServiceTests
{
    private static PassPlatDbContext CrearContext() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (InicioService Servicio, Mock<IAuditoriaPwdService> Auditoria, Mock<IBackgroundStatusService> Background) CrearServicio(PassPlatDbContext ctx)
    {
        var uow = Mock.Of<IUnitOfWorkAsync>();

        var auditoria = new Mock<IAuditoriaPwdService>();
        auditoria.Setup(a => a.ObtenerPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<AuditoriaPwdDto>>.Success(Array.Empty<AuditoriaPwdDto>()));

        var background = new Mock<IBackgroundStatusService>();
        background.Setup(b => b.GetBackgroundJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<BackgroundJobDto>>.Success([]));

        var servicio = new InicioService(
            new UsuarioRepository(ctx, uow),
            new SesionRepository(ctx, uow, NullLogger<SesionRepository>.Instance),
            new DispConfiableRepository(ctx),
            new MFARepository(ctx, uow),
            new HistorialPwdRepository(ctx),
            new TenantRepository(ctx),
            new AppRepository(ctx, Mock.Of<ICacheService>(), Mock.Of<ILoggerService>()),
            auditoria.Object,
            background.Object);

        return (servicio, auditoria, background);
    }

    /// <summary>Usuario 5 en T1 (actor), usuario 6 en T1 y usuario 7 en T2.</summary>
    private static async Task SembrarTenantsAsync(PassPlatDbContext ctx)
    {
        ctx.Usuarios.AddRange(
            new Usuario { Id = 5, IdTenant = 1, IdEstado = 1, NomUsuario = "u5", Nombre = "U", Apellido = "Cinco" },
            new Usuario { Id = 6, IdTenant = 1, IdEstado = 1, NomUsuario = "u6", Nombre = "U", Apellido = "Seis" },
            new Usuario { Id = 7, IdTenant = 2, IdEstado = 1, NomUsuario = "u7", Nombre = "U", Apellido = "Siete" });

        ctx.MFA.AddRange(
            new MFA { Id = 1, IdUsuario = 5, IdTenant = 1, IdTipoMFA = 1, IdMFA = "m1", IdEstado = 1 },
            new MFA { Id = 2, IdUsuario = 5, IdTenant = 1, IdTipoMFA = 2, IdMFA = "m2", IdEstado = 1 },
            new MFA { Id = 3, IdUsuario = 5, IdTenant = 1, IdTipoMFA = 1, IdMFA = "m3", IdEstado = 3 },
            new MFA { Id = 4, IdUsuario = 6, IdTenant = 1, IdTipoMFA = 1, IdMFA = "m4", IdEstado = 1 });

        ctx.DispConfiables.AddRange(
            new DispConfiable { Id = 1, IdUsuario = 5, IdTenant = 1, IdDisp = 1, Confiable = true },
            new DispConfiable { Id = 2, IdUsuario = 5, IdTenant = 1, IdDisp = 2, Confiable = false },
            new DispConfiable { Id = 3, IdUsuario = 5, IdTenant = 2, IdDisp = 1, Confiable = true },
            new DispConfiable { Id = 4, IdUsuario = 6, IdTenant = 1, IdDisp = 1, Confiable = true });

        var vigente = DateTime.Now.AddHours(2);
        ctx.Sesiones.AddRange(
            new Sesion { Id = Guid.NewGuid(), IdUsuario = 5, IdTenant = 1, IdApp = 1, IdTokenExt = "t1", FecExpira = vigente, EsActiva = true },
            new Sesion { Id = Guid.NewGuid(), IdUsuario = 5, IdTenant = 1, IdApp = 1, IdTokenExt = "t2", FecExpira = vigente, EsActiva = true },
            new Sesion { Id = Guid.NewGuid(), IdUsuario = 5, IdTenant = 1, IdApp = 1, IdTokenExt = "t3", FecExpira = DateTime.Now.AddHours(-2), EsActiva = true },
            new Sesion { Id = Guid.NewGuid(), IdUsuario = 5, IdTenant = 2, IdApp = 1, IdTokenExt = "t4", FecExpira = vigente, EsActiva = true },
            new Sesion { Id = Guid.NewGuid(), IdUsuario = 6, IdTenant = 1, IdApp = 1, IdTokenExt = "t5", FecExpira = vigente, EsActiva = true });

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ObtenerAsync_SinPermisos_DevuelveCuentaYTenantSinValores()
    {
        var ctx = CrearContext();
        await SembrarTenantsAsync(ctx);
        var (servicio, _, _) = CrearServicio(ctx);

        var result = await servicio.ObtenerAsync(5, 1, new HashSet<string>(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = result.Value;
        Assert.NotNull(dto.Tenant);
        Assert.Null(dto.Tenant.Usuarios);
        Assert.Null(dto.Tenant.SesionesActivas);
        Assert.Null(dto.Tenant.ConIntentosExcedidos);
        Assert.Null(dto.Tenant.ConPasswordExpirada);
        Assert.Null(dto.Tenant.DispConfiables);
        Assert.Null(dto.Tenant.DispNoConfiables);
        Assert.Null(dto.Tenant.Actividad);
        Assert.Null(dto.Plataforma);
        Assert.Equal(2, dto.Cuenta.MetodosMfaActivos);
    }

    [Fact]
    public async Task ObtenerAsync_AdminTenant_CuentaSoloSuTenant()
    {
        var ctx = CrearContext();
        await SembrarTenantsAsync(ctx);
        var (servicio, _, _) = CrearServicio(ctx);

        var permisos = new HashSet<string> { "USUARIOS_VER", "SESIONES_VER", "USUARIOS_VERDISP" };
        var result = await servicio.ObtenerAsync(5, 1, permisos, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = result.Value;
        Assert.Null(dto.Plataforma);
        Assert.NotNull(dto.Tenant);
        Assert.Equal(2, dto.Tenant.Usuarios);
        Assert.Equal(3, dto.Tenant.SesionesActivas);
        Assert.Equal(2, dto.Tenant.DispConfiables);
        Assert.Equal(1, dto.Tenant.DispNoConfiables);
        Assert.Equal(2, dto.Cuenta.SesionesActivas);
        Assert.Equal(1, dto.Cuenta.DispositivosConfiables);
    }

    [Fact]
    public async Task ObtenerAsync_AdminTenant_PideSeisEventosDelTenant()
    {
        var ctx = CrearContext();
        await SembrarTenantsAsync(ctx);
        var (servicio, auditoria, _) = CrearServicio(ctx);

        var permisos = new HashSet<string> { "AUDITORIA_VER" };
        var result = await servicio.ObtenerAsync(5, 1, permisos, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(result.Value.Tenant);
        Assert.NotNull(result.Value.Tenant.Actividad);
        auditoria.Verify(a => a.ObtenerPorTenantAsync(1, 6, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerAsync_Plataforma_DevuelveTotalesGlobalesSinTenant()
    {
        var ctx = CrearContext();
        ctx.Tenants.AddRange(
            new Tenant { Id = 1, Codigo = "T1", Nombre = "Uno", Activo = true },
            new Tenant { Id = 2, Codigo = "T2", Nombre = "Dos", Activo = true },
            new Tenant { Id = 3, Codigo = "T3", Nombre = "Tres", Activo = false });
        ctx.Apps.AddRange(
            new App { Id = 1, Codigo = "A1", Nombre = "App uno" },
            new App { Id = 2, Codigo = "A2", Nombre = "App dos" },
            new App { Id = 3, Codigo = "A3", Nombre = "App tres" });
        ctx.Usuarios.AddRange(
            new Usuario { Id = 1, IdTenant = 1, IdEstado = 1, NomUsuario = "a" },
            new Usuario { Id = 2, IdTenant = 2, IdEstado = 1, NomUsuario = "b" },
            new Usuario { Id = 3, IdTenant = 3, IdEstado = 1, NomUsuario = "c" });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        var (servicio, _, background) = CrearServicio(ctx);
        background.Setup(b => b.GetBackgroundJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<BackgroundJobDto>>.Success(
                [new BackgroundJobDto { Nombre = "EmailBackground", Estado = "Activo" }]));

        var permisos = new HashSet<string> { "TENANTS_VER", "APPS_VER", "USUARIOS_VER" };
        var result = await servicio.ObtenerAsync(1, null, permisos, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = result.Value;
        Assert.Null(dto.Tenant);
        Assert.NotNull(dto.Plataforma);
        Assert.Equal(2, dto.Plataforma.TenantsActivos);
        Assert.Equal(3, dto.Plataforma.Apps);
        Assert.Equal(3, dto.Plataforma.Usuarios);
        Assert.NotNull(dto.Plataforma.Servicios);
        Assert.Equal("EmailBackground", Assert.Single(dto.Plataforma.Servicios).Nombre);
    }

    [Fact]
    public async Task ObtenerAsync_PwdActual_DevuelveFecExpira()
    {
        var ctx = CrearContext();
        ctx.Usuarios.Add(new Usuario { Id = 5, IdTenant = 1, IdEstado = 1, NomUsuario = "u5" });
        var expira = new DateTime(2027, 1, 15, 8, 30, 0);
        ctx.HistorialPwd.AddRange(
            new HistorialPwd { Id = 1, IdUsuario = 5, IdPolitica = 1, HashPwd = "h1", EsActual = true, FecExpira = expira },
            new HistorialPwd { Id = 2, IdUsuario = 5, IdPolitica = 1, HashPwd = "h2", EsActual = false, FecExpira = new DateTime(2030, 1, 1) });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        var (servicio, _, _) = CrearServicio(ctx);

        var result = await servicio.ObtenerAsync(5, 1, new HashSet<string>(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(expira, result.Value.Cuenta.FecExpiraPwd);
    }

    [Fact]
    public async Task ObtenerAsync_FalloDeServicio_PropagaError()
    {
        var ctx = CrearContext();
        await SembrarTenantsAsync(ctx);
        var (servicio, auditoria, _) = CrearServicio(ctx);
        auditoria.Setup(a => a.ObtenerPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<AuditoriaPwdDto>>.Failure("AUD_FAIL", "sin auditoria"));

        var permisos = new HashSet<string> { "AUDITORIA_VER" };
        var result = await servicio.ObtenerAsync(5, 1, permisos, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("AUD_FAIL", result.Error!.Code);
    }
}
