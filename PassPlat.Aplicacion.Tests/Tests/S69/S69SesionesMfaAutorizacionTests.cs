using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests.Tests.S69;

/// <summary>
/// S69 · D1, D2 — autorizacion de <c>GET api/sesiones/activas/{idUsuario}</c> y <c>GET api/mfa/metodos/{idUsuario}</c>:
/// uno mismo ve sus datos; otro usuario solo si es miembro activo del tenant del JWT.
/// </summary>
public class S69SesionesMfaAutorizacionTests
{
    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static (SesionService service, PassPlatDbContext ctx, Mock<IUsuarioTenantRepository> repoMt) BuildSesion(
        Result<UsuarioTenant?> resultadoMembresia)
    {
        var ctx = CrearContext();
        var repo = new SesionRepository(ctx, Mock.Of<IUnitOfWorkAsync>(), NullLogger<SesionRepository>.Instance);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IReadOnlyList<SesionDto>>(It.IsAny<IReadOnlyList<Sesion>>()))
            .Returns((IReadOnlyList<Sesion> src) => src.Select(s => new SesionDto
            {
                Id = s.Id,
                IdUsuario = s.IdUsuario,
                IdTenant = s.IdTenant,
                IdApp = s.IdApp,
                IdTokenExt = s.IdTokenExt,
                FecInicio = s.FecInicio,
                UltActividad = s.UltActividad,
                FecExpira = s.FecExpira,
                EsActiva = s.EsActiva
            }).ToList());

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoMembresia);

        var service = new SesionService(
            repo,
            mapper.Object,
            usuarioTenantRepo.Object,
            Mock.Of<IUnitOfWorkAsync>());

        return (service, ctx, usuarioTenantRepo);
    }

    private static (MFAService service, PassPlatDbContext ctx, Mock<IUsuarioTenantRepository> repoMt) BuildMfa(
        Result<UsuarioTenant?> resultadoMembresia)
    {
        var ctx = CrearContext();
        var repo = new MFARepository(ctx, Mock.Of<IUnitOfWorkAsync>());

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IReadOnlyList<MFADto>>(It.IsAny<IReadOnlyList<MFA>>()))
            .Returns((IReadOnlyList<MFA> src) => src.Select(m => new MFADto
            {
                Id = m.Id,
                IdUsuario = m.IdUsuario,
                IdTipoMFA = m.IdTipoMFA,
                IdMFA = m.IdMFA,
                EsPrincipal = m.EsPrincipal,
                IdEstado = m.IdEstado,
                FecAlta = m.FecAlta
            }).ToList());

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoMembresia);

        var service = new MFAService(
            repo,
            mapper.Object,
            Mock.Of<IMfaCodeStore>(),
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<IEmailQueue>(),
            NullLogger<MFAService>.Instance,
            usuarioTenantRepo.Object);

        return (service, ctx, usuarioTenantRepo);
    }

    private static async Task<Sesion> SembrarSesionAsync(PassPlatDbContext ctx, Sesion s)
    {
        if (!ctx.Usuarios.Any(u => u.Id == s.IdUsuario))
        {
            ctx.Usuarios.Add(new Usuario
            {
                Id = s.IdUsuario,
                IdTenant = 1,
                IdEstado = 1,
                NomUsuario = $"user{s.IdUsuario}",
                Email = $"user{s.IdUsuario}@example.com",
                Nombre = $"User {s.IdUsuario}",
                Apellido = $"Surname {s.IdUsuario}",
                ReqCambioPwd = true,
                IntentosFallidos = 0,
                Eliminado = false,
                FecCrea = DateTime.UtcNow,
                FecMod = DateTime.UtcNow
            });
        }
        if (!ctx.Tenants.Any(t => t.Id == s.IdTenant))
        {
            ctx.Tenants.Add(new Tenant { Id = s.IdTenant, Nombre = $"tenant{s.IdTenant}", Activo = true });
        }
        if (!ctx.Apps.Any(a => a.Id == s.IdApp))
        {
            ctx.Apps.Add(new App { Id = s.IdApp, Nombre = $"app{s.IdApp}", Activa = true });
        }
        ctx.Sesiones.Add(s);
        await ctx.SaveChangesAsync();
        return s;
    }

    private static async Task<MFA> SembrarMfaAsync(PassPlatDbContext ctx, MFA m)
    {
        if (!ctx.Usuarios.Any(u => u.Id == m.IdUsuario))
        {
            ctx.Usuarios.Add(new Usuario
            {
                Id = m.IdUsuario,
                IdTenant = 1,
                IdEstado = 1,
                NomUsuario = $"user{m.IdUsuario}",
                Email = $"user{m.IdUsuario}@example.com",
                Nombre = $"User {m.IdUsuario}",
                Apellido = $"Surname {m.IdUsuario}",
                ReqCambioPwd = true,
                IntentosFallidos = 0,
                Eliminado = false,
                FecCrea = DateTime.UtcNow,
                FecMod = DateTime.UtcNow
            });
        }
        if (!ctx.TiposMFA.Any(t => t.Id == m.IdTipoMFA))
        {
            ctx.TiposMFA.Add(new TipoMFA { Id = m.IdTipoMFA, Nombre = "Test", Activo = true });
        }
        ctx.MFA.Add(m);
        await ctx.SaveChangesAsync();
        return m;
    }

    [Fact]
    public async Task Sesiones_uno_mismo_no_consulta_membresia()
    {
        var (service, ctx, repoMt) = BuildSesion(Result<UsuarioTenant?>.Success(null, allowNull: true));
        await SembrarSesionAsync(ctx, new Sesion
        {
            Id = Guid.NewGuid(),
            IdUsuario = 5,
            IdTenant = 1,
            IdApp = 1,
            IdTokenExt = "token1",
            FecExpira = DateTime.UtcNow.AddHours(1),
            HashRefresh = null,
            IdDisp = null,
            IdIP = null,
            IdSesionPadre = null,
            EsActiva = true,
            FecInicio = DateTime.UtcNow
        });

        var result = await service.ObtenerSesionesVisiblesAsync(5, 5, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Single(result.Value);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sesiones_de_un_miembro_activo_del_tenant_son_visibles()
    {
        var (service, ctx, repoMt) = BuildSesion(Result<UsuarioTenant?>.Success(
            new UsuarioTenant { Id = 31, IdUsuario = 2, IdTenant = 1, Activo = true }, allowNull: true));
        await SembrarSesionAsync(ctx, new Sesion
        {
            Id = Guid.NewGuid(),
            IdUsuario = 2,
            IdTenant = 1,
            IdApp = 1,
            IdTokenExt = "token2",
            FecExpira = DateTime.UtcNow.AddHours(1),
            HashRefresh = null,
            IdDisp = null,
            IdIP = null,
            IdSesionPadre = null,
            EsActiva = true,
            FecInicio = DateTime.UtcNow
        });

        var result = await service.ObtenerSesionesVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Single(result.Value);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sesiones_de_un_no_miembro_devuelven_FORBIDDEN()
    {
        var (service, _, _) = BuildSesion(Result<UsuarioTenant?>.Success(null, allowNull: true));

        var result = await service.ObtenerSesionesVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatusCode);
    }

    [Fact]
    public async Task Sesiones_falla_de_BD_en_membresia_se_propaga()
    {
        var (service, _, _) = BuildSesion(Result<UsuarioTenant?>.Failure("DB_ERROR", "falla de base de datos"));

        var result = await service.ObtenerSesionesVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DB_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task Mfa_uno_mismo_no_consulta_membresia()
    {
        var (service, ctx, repoMt) = BuildMfa(Result<UsuarioTenant?>.Success(null, allowNull: true));
        await SembrarMfaAsync(ctx, new MFA
        {
            Id = 101,
            IdUsuario = 5,
            IdTipoMFA = 1,
            IdMFA = "mfa1",
            EsPrincipal = true,
            IdEstado = 1,
            FecAlta = DateTime.UtcNow
        });

        var result = await service.ObtenerMetodosVisiblesAsync(5, 5, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Single(result.Value);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mfa_de_un_miembro_activo_del_tenant_es_visible()
    {
        var (service, ctx, repoMt) = BuildMfa(Result<UsuarioTenant?>.Success(
            new UsuarioTenant { Id = 31, IdUsuario = 2, IdTenant = 1, Activo = true }, allowNull: true));
        await SembrarMfaAsync(ctx, new MFA
        {
            Id = 102,
            IdUsuario = 2,
            IdTipoMFA = 1,
            IdMFA = "mfa2",
            EsPrincipal = true,
            IdEstado = 1,
            FecAlta = DateTime.UtcNow
        });

        var result = await service.ObtenerMetodosVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Single(result.Value);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mfa_de_un_no_miembro_devuelve_FORBIDDEN()
    {
        var (service, _, _) = BuildMfa(Result<UsuarioTenant?>.Success(null, allowNull: true));

        var result = await service.ObtenerMetodosVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatusCode);
    }
}