using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Catalogos;
using Xunit;

namespace PassPlat.Aplicacion.Tests.S116;

/// <summary>S116 · D1, D2 — edición y baja de roles por EF con alcance de tenant; códigos de SP_Rol_Crear.</summary>
public sealed class S116RolesEscrituraTests
{
    private const int RolT5 = 10;
    private const int RolT6 = 11;
    private const int RolGlobal = 12;

    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static PassPlatDbContext Sembrar()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        ctx.Roles.AddRange(
            new Rol { Id = RolT5, IdTenant = 5, Codigo = "T5_ROL", Nombre = "Rol T5", Activo = true },
            new Rol { Id = RolT6, IdTenant = 6, Codigo = "T6_ROL", Nombre = "Rol T6", Activo = true },
            new Rol { Id = RolGlobal, IdTenant = null, Codigo = "GLOBAL", Nombre = "Global", Activo = true });
        ctx.SaveChanges();
        return ctx;
    }

    private static RolService Servicio(PassPlatDbContext ctx, Mock<IUnitOfWorkAsync> uow) =>
        new(new RolRepository(ctx, uow.Object), uow.Object, Mapper);

    [Theory]
    [InlineData(RolT5, 5, null)]
    [InlineData(RolT5, null, null)]
    [InlineData(RolGlobal, null, null)]
    [InlineData(RolT6, 5, 404)]
    [InlineData(RolGlobal, 5, 403)]
    [InlineData(99, 5, 404)]
    public async Task Desactivar_respeta_el_alcance_del_tenant(int idRol, int? alcance, int? statusEsperado)
    {
        using var ctx = Sembrar();
        var uow = new Mock<IUnitOfWorkAsync>();

        var r = await Servicio(ctx, uow).DesactivarAsync(idRol, alcance, TestContext.Current.CancellationToken);

        if (statusEsperado is null)
        {
            Assert.True(r.IsSuccess);
            Assert.False(ctx.Roles.Single(x => x.Id == idRol).Activo);
            uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            Assert.True(r.IsFailure);
            Assert.Equal(statusEsperado, r.Error!.HttpStatusCode);
            Assert.All(ctx.Roles, x => Assert.True(x.Activo));
            uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Actualizar_cambia_nombre_descripcion_y_estado_y_guarda()
    {
        using var ctx = Sembrar();
        var uow = new Mock<IUnitOfWorkAsync>();

        var r = await Servicio(ctx, uow).ActualizarAsync(RolT5, "Nuevo", "Desc", false, 5, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Equal("Nuevo", r.Value.Nombre);
        var rol = ctx.Roles.Single(x => x.Id == RolT5);
        Assert.Equal(("Nuevo", "Desc", false), (rol.Nombre, rol.Descripcion, rol.Activo));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(2, "CODIGO_DUPLICADO", 409)]
    [InlineData(1, "DATOS_INVALIDOS", 400)]
    [InlineData(3, "DATOS_INVALIDOS", 400)]
    [InlineData(-1, "DB_ERROR", 500)]
    public async Task Crear_traduce_el_resultado_del_SP_a_su_estado_HTTP(int resultado, string codigo, int status)
    {
        using var ctx = Sembrar();
        var raw = new Mock<IRawQueryRepositoryAsync>();
        raw.Setup(q => q.QuerySPAsync<CrearRolResult>("SP_Rol_Crear", It.IsAny<IEnumerable<RawParameter>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CrearRolResult>>.Success(new List<CrearRolResult> { new() { Resultado = resultado, Mensaje = "mensaje del SP" } }));
        var uow = new Mock<IUnitOfWorkAsync>();
        uow.SetupGet(u => u.RawQuery).Returns(raw.Object);

        var r = await Servicio(ctx, uow).CrearAsync(new CrearRolDto { IdTenant = 5, Codigo = "T5_ROL", Nombre = "Dup" }, null, TestContext.Current.CancellationToken);

        Assert.True(r.IsFailure);
        Assert.Equal((codigo, status), (r.Error!.Code, r.Error.HttpStatusCode));
        if (resultado < 0) Assert.DoesNotContain("mensaje del SP", r.Error.Message);
    }
}
