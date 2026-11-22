using AutoMapper;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Logging.Interfaces;
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

namespace PassPlat.Aplicacion.Tests.Tests.S68;

/// <summary>
/// S68.2 · K4 (D6) — autorizacion de <c>GET api/bloqueos/usuario/{idUsuario}</c>:
/// uno mismo ve sus bloqueos; otro usuario solo si es miembro activo del tenant del JWT.
/// </summary>
public class S682BloqueosAutorizacionTests
{
    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static Bloqueo NuevoBloqueo(int id, int idUsuario, int idTenant)
    {
        var b = Bloqueo.Crear(idUsuario, idTenant, 1, "Prueba de autorizacion");
        b.Id = id;
        b.Activo = true;
        return b;
    }

    private static UsuarioTenant Membresia(int id, int idUsuario, int idTenant) =>
        new()
        {
            Id = id,
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            Activo = true
        };

    private static (BloqueoService service, PassPlatDbContext ctx, Mock<IUsuarioTenantRepository> repoMt) Build(
        Result<UsuarioTenant?> resultadoMembresia)
    {
        var ctx = CrearContext();
        var repo = new BloqueoRepository(ctx);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IReadOnlyList<BloqueoDto>>(It.IsAny<IReadOnlyList<Bloqueo>>()))
            .Returns((IReadOnlyList<Bloqueo> src) => src.Select(b => new BloqueoDto
            {
                Id = b.Id,
                IdUsuario = b.IdUsuario,
                IdTenant = b.IdTenant,
                IdTipoBloqueo = b.IdTipoBloqueo,
                Motivo = b.Motivo,
                Activo = b.Activo
            }).ToList());

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoMembresia);

        var service = new BloqueoService(
            repo,
            mapper.Object,
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<IEmailQueue>(),
            NullLogger<BloqueoService>.Instance,
            Mock.Of<ILoggerService>(),
            new HttpContextAccessor(),
            usuarioTenantRepo.Object);
        return (service, ctx, usuarioTenantRepo);
    }

    private static async Task<Bloqueo> SembrarAsync(PassPlatDbContext ctx, Bloqueo b)
    {
        if (!ctx.TiposBloqueo.Any(t => t.Id == b.IdTipoBloqueo))
        {
            ctx.TiposBloqueo.Add(new TipoBloqueo { Id = b.IdTipoBloqueo, Nombre = "Test", EsTemporal = false });
            await ctx.SaveChangesAsync();
        }
        ctx.Bloqueos.Add(b);
        await ctx.SaveChangesAsync();
        return b;
    }

    [Fact]
    public async Task Uno_mismo_ve_sus_bloqueos_sin_consultar_membresia()
    {
        var (service, ctx, repoMt) = Build(Result<UsuarioTenant?>.Success(null, allowNull: true));
        await SembrarAsync(ctx, NuevoBloqueo(101, 5, 1));

        var result = await service.ObtenerBloqueosVisiblesAsync(5, 5, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var bloqueo = Assert.Single(result.Value);
        Assert.Equal(101, bloqueo.Id);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_ve_bloqueos_de_un_miembro_activo_del_tenant()
    {
        var (service, ctx, repoMt) = Build(Result<UsuarioTenant?>.Success(Membresia(31, 2, 1), allowNull: true));
        await SembrarAsync(ctx, NuevoBloqueo(102, 2, 1));

        var result = await service.ObtenerBloqueosVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var bloqueo = Assert.Single(result.Value);
        Assert.Equal(102, bloqueo.Id);
        repoMt.Verify(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_recibe_FORBIDDEN_si_el_usuario_no_es_miembro()
    {
        var (service, _, _) = Build(Result<UsuarioTenant?>.Success(null, allowNull: true));

        var result = await service.ObtenerBloqueosVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatusCode);
    }

    [Fact]
    public async Task Falla_de_BD_en_membresia_se_propaga()
    {
        var (service, _, _) = Build(Result<UsuarioTenant?>.Failure("DB_ERROR", "falla de base de datos"));

        var result = await service.ObtenerBloqueosVisiblesAsync(1, 2, 1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DB_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task Solo_devuelve_bloqueos_del_tenant_pedido()
    {
        var (service, ctx, _) = Build(Result<UsuarioTenant?>.Success(null, allowNull: true));
        await SembrarAsync(ctx, NuevoBloqueo(103, 7, 1));
        await SembrarAsync(ctx, NuevoBloqueo(104, 7, 2));

        var result = await service.ObtenerBloqueosVisiblesAsync(7, 7, 1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var bloqueo = Assert.Single(result.Value);
        Assert.Equal(103, bloqueo.Id);
        Assert.Equal(1, bloqueo.IdTenant);
    }
}
