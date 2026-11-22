using System.Security.Claims;
using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
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

namespace PassPlat.Aplicacion.Tests.Tests.S61;

/// <summary>
/// S61 - K4 (D2): la asignacion de accesos exige membresia activa en el tenant y solo
/// crea un acceso de plataforma (IdUsuarioTenant NULL) en el tenant EsSistema, con rol
/// global y asignador con is_system.
/// </summary>
public class S61AccesoServiceTests
{
    private static PassPlatDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PassPlatDbContext(options);
    }

    private static UsuarioTenant Membresia(int id, int idUsuario, int idTenant, bool esSistema)
        => new()
        {
            Id = id,
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            Activo = true,
            Tenant = new Tenant { Id = idTenant, Codigo = esSistema ? "PLATFORM" : "ABARROTES", EsSistema = esSistema }
        };

    private static (AccesoService service, PassPlatDbContext ctx) Build(
        Rol rol, UsuarioTenant? membresia, bool isSystem)
    {
        var ctx = CrearContext();
        var repo = new AccesoRepository(ctx);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<AccesoDto>(It.IsAny<Acceso>()))
            .Returns((Acceso a) => new AccesoDto
            {
                Id = a.Id,
                IdUsuario = a.IdUsuario,
                IdTenant = a.IdTenant,
                IdApp = a.IdApp,
                IdRol = a.IdRol,
                Activo = a.Activo
            });

        var rolRepo = new Mock<IRolRepository>();
        rolRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Rol>.Success(rol));

        var usuarioTenantRepo = new Mock<IUsuarioTenantRepository>();
        usuarioTenantRepo
            .Setup(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(membresia, allowNull: true));

        var uow = new FakeUnitOfWork(ctx);

        var claims = isSystem ? new[] { new Claim("is_system", "true") } : Array.Empty<Claim>();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

        var service = new AccesoService(repo, mapper.Object, Mock.Of<IUsuarioRepository>(), rolRepo.Object,
            usuarioTenantRepo.Object, Mock.Of<IAppRepository>(), Mock.Of<IEmailQueue>(),
            NullLogger<AccesoService>.Instance, uow, accessor);
        return (service, ctx);
    }

    private static AsignarAccesoDto Dto(int idTenant = 2, int idRol = 5) => new()
    {
        IdUsuario = 9,
        IdTenant = idTenant,
        IdApp = 12,
        IdRol = idRol
    };

    [Fact]
    public async Task Con_membresia_activa_enlaza_el_acceso_a_la_membresia()
    {
        var (service, ctx) = Build(
            new Rol { Id = 5, Codigo = "ABARROTES_ADMIN", Activo = true, IdTenant = 2 },
            Membresia(31, 9, 2, esSistema: false), isSystem: false);

        var result = await service.AsignarAccesoAsync(Dto(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var acceso = Assert.Single(ctx.Accesos);
        Assert.Equal(31, acceso.IdUsuarioTenant);
    }

    [Fact]
    public async Task Sin_membresia_activa_devuelve_SIN_MEMBRESIA_TENANT_y_no_escribe()
    {
        var (service, ctx) = Build(
            new Rol { Id = 5, Codigo = "ABARROTES_ADMIN", Activo = true, IdTenant = 2 },
            membresia: null, isSystem: false);

        var result = await service.AsignarAccesoAsync(Dto(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("SIN_MEMBRESIA_TENANT", result.Error!.Code);
        Assert.Equal("El usuario no pertenece a este tenant", result.Error.Message);
        Assert.Equal(400, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(ctx.Accesos);
    }

    [Fact]
    public async Task Rol_inexistente_o_inactivo_devuelve_ROL_NO_ENCONTRADO_y_no_escribe()
    {
        var (service, ctx) = Build(
            new Rol { Id = 7, Codigo = "ROL_INACTIVO", Activo = false, IdTenant = 2 },
            membresia: null, isSystem: false);

        var result = await service.AsignarAccesoAsync(Dto(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ROL_NO_ENCONTRADO", result.Error!.Code);
        Assert.Equal("El rol no existe o no está activo", result.Error.Message);
        Assert.Equal(400, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(ctx.Accesos);
    }

    [Fact]
    public async Task Rol_de_otro_tenant_devuelve_ROL_OTRO_TENANT_y_no_escribe()
    {
        var (service, ctx) = Build(
            new Rol { Id = 9, Codigo = "VESTUARIO_ADMIN", Activo = true, IdTenant = 3 },
            Membresia(32, 9, 2, esSistema: false), isSystem: false);

        var result = await service.AsignarAccesoAsync(Dto(idTenant: 2, idRol: 9), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("ROL_OTRO_TENANT", result.Error!.Code);
        Assert.Equal("El rol seleccionado pertenece a otro tenant", result.Error.Message);
        Assert.Equal(400, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(ctx.Accesos);
    }

    [Fact]
    public async Task Tenant_de_plataforma_con_rol_global_y_asignador_sistema_crea_acceso_sin_membresia()
    {
        var (service, ctx) = Build(
            new Rol { Id = 1, Codigo = "PLATFORM_ADMIN", Activo = true, IdTenant = null },
            Membresia(33, 9, 1, esSistema: true), isSystem: true);

        var result = await service.AsignarAccesoAsync(Dto(idTenant: 1, idRol: 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var acceso = Assert.Single(ctx.Accesos);
        Assert.Null(acceso.IdUsuarioTenant);
    }

    [Fact]
    public async Task Tenant_de_plataforma_sin_asignador_sistema_enlaza_la_membresia()
    {
        var (service, ctx) = Build(
            new Rol { Id = 1, Codigo = "PLATFORM_ADMIN", Activo = true, IdTenant = null },
            Membresia(33, 9, 1, esSistema: true), isSystem: false);

        var result = await service.AsignarAccesoAsync(Dto(idTenant: 1, idRol: 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var acceso = Assert.Single(ctx.Accesos);
        Assert.Equal(33, acceso.IdUsuarioTenant);
    }

    [Fact]
    public async Task Rol_global_en_tenant_normal_enlaza_la_membresia()
    {
        var (service, ctx) = Build(
            new Rol { Id = 1, Codigo = "PLATFORM_ADMIN", Activo = true, IdTenant = null },
            Membresia(34, 9, 2, esSistema: false), isSystem: true);

        var result = await service.AsignarAccesoAsync(Dto(idTenant: 2, idRol: 1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var acceso = Assert.Single(ctx.Accesos);
        Assert.Equal(34, acceso.IdUsuarioTenant);
    }
}
