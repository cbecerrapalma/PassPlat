using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests.Tests.S108;

/// <summary>
/// S108 · D3 — RolesHerenciaService valida antes de escribir lo que TR_RolesHerencia_ValidarTenant
/// rechazaba con un 500: rol de otro tenant, rol inexistente y ciclo responden 400 sin agregar la fila.
/// Semilla: 1 global, 2 y 3 del tenant, 4 de otro tenant; 3 hereda de 2.
/// </summary>
public sealed class S108HerenciaValidacionTests
{
    private const int Tenant = 2;
    private const int OtroTenant = 4;

    private readonly Mock<IUnitOfWorkAsync> _uow = new();
    private readonly Mock<IMapper> _mapper = new();

    public S108HerenciaValidacionTests()
    {
        _mapper.Setup(m => m.Map<RolesHerencia>(It.IsAny<object>()))
            .Returns((object origen) =>
            {
                var d = (CrearRolesHerenciaDto)origen;
                return new RolesHerencia { IdRolHijo = d.IdRolHijo, IdRolPadre = d.IdRolPadre, IdTenant = d.IdTenant };
            });
        _mapper.Setup(m => m.Map<RolesHerenciaDto>(It.IsAny<object>())).Returns(new RolesHerenciaDto());
    }

    private static PassPlatDbContext Contexto(string bd) =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(bd).Options);

    private static async Task<string> SembrarAsync()
    {
        var bd = Guid.NewGuid().ToString();
        await using var ctx = Contexto(bd);
        ctx.Set<Rol>().AddRange(
            new Rol { Id = 1, Codigo = "S108_GLOBAL", Nombre = "Global", IdTenant = null },
            new Rol { Id = 2, Codigo = "S108_A", Nombre = "Propio A", IdTenant = Tenant },
            new Rol { Id = 3, Codigo = "S108_B", Nombre = "Propio B", IdTenant = Tenant },
            new Rol { Id = 4, Codigo = "S108_AJENO", Nombre = "Ajeno", IdTenant = OtroTenant });
        ctx.Set<RolesHerencia>().Add(new RolesHerencia { Id = 10, IdRolHijo = 3, IdRolPadre = 2, IdTenant = Tenant });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bd;
    }

    private RolesHerenciaService Servicio(PassPlatDbContext ctx) =>
        new(new RolesHerenciaRepository(ctx), new RolRepository(ctx, _uow.Object), _uow.Object, _mapper.Object);

    private static CrearRolesHerenciaDto Dto(int hijo, int padre) => new() { IdRolHijo = hijo, IdRolPadre = padre, IdTenant = Tenant };

    private void AssertRechazoSinEscribir(PassPlatDbContext ctx, Result<RolesHerenciaDto> r, string codigo)
    {
        Assert.True(r.IsFailure);
        Assert.Equal(codigo, r.Error!.Code);
        Assert.Equal(ErrorType.Validation, r.Error.Type);
        Assert.DoesNotContain(ctx.ChangeTracker.Entries<RolesHerencia>(), e => e.State == EntityState.Added);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_con_un_rol_de_otro_tenant_devuelve_ROL_OTRO_TENANT_sin_escribir()
    {
        await using var ctx = Contexto(await SembrarAsync());
        var r = await Servicio(ctx).CrearAsync(Dto(2, 4), TestContext.Current.CancellationToken);
        AssertRechazoSinEscribir(ctx, r, "ROL_OTRO_TENANT");
    }

    [Fact]
    public async Task Crear_con_un_rol_inexistente_devuelve_ROL_NO_ENCONTRADO_sin_escribir()
    {
        await using var ctx = Contexto(await SembrarAsync());
        var r = await Servicio(ctx).CrearAsync(Dto(2, 99), TestContext.Current.CancellationToken);
        AssertRechazoSinEscribir(ctx, r, "ROL_NO_ENCONTRADO");
    }

    [Fact]
    public async Task Crear_que_cierra_un_ciclo_devuelve_HERENCIA_CICLO_sin_escribir()
    {
        await using var ctx = Contexto(await SembrarAsync());
        var r = await Servicio(ctx).CrearAsync(Dto(2, 3), TestContext.Current.CancellationToken);
        AssertRechazoSinEscribir(ctx, r, "HERENCIA_CICLO");
    }

    [Fact]
    public async Task Crear_con_el_mismo_rol_como_hijo_y_padre_devuelve_HERENCIA_CICLO_sin_escribir()
    {
        await using var ctx = Contexto(await SembrarAsync());
        var r = await Servicio(ctx).CrearAsync(Dto(2, 2), TestContext.Current.CancellationToken);
        AssertRechazoSinEscribir(ctx, r, "HERENCIA_CICLO");
    }

    [Fact]
    public async Task Crear_con_un_rol_del_tenant_y_uno_global_agrega_y_guarda_una_vez()
    {
        await using var ctx = Contexto(await SembrarAsync());
        var r = await Servicio(ctx).CrearAsync(Dto(3, 1), TestContext.Current.CancellationToken);
        Assert.True(r.IsSuccess);
        Assert.Contains(ctx.ChangeTracker.Entries<RolesHerencia>(),
            e => e.State == EntityState.Added && e.Entity.IdRolHijo == 3 && e.Entity.IdRolPadre == 1);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
