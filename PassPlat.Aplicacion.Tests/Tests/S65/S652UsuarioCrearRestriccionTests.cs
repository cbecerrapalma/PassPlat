using AutoMapper;
using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S65;

public class S652UsuarioCrearRestriccionTests
{
    [Fact]
    public async Task CrearAsync_CuandoFallaRestriccion_RetornaDbConstraintSinDetalleTecnico()
    {
        var (service, uow) = CrearServicio();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("x", new Exception("y")));

        var result = await service.CrearAsync(CrearDto(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_CONSTRAINT", result.Error!.Code);
        Assert.Equal(400, result.Error.HttpStatusCode);
        Assert.Equal(CBP.Results.Errors.ErrorType.Validation, result.Error.Type);
        Assert.Equal("No se pudo guardar el usuario: los datos no cumplen una restricción de la base de datos.", result.Error.Message);
        Assert.DoesNotContain("x", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("y", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrearAsync_CuandoGuarda_RetornaExitoYConfirmaUnaVez()
    {
        var (service, uow) = CrearServicio();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await service.CrearAsync(CrearDto(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (UsuarioService Service, Mock<IUnitOfWorkAsync> Uow) CrearServicio()
    {
        var options = new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new PassPlatDbContext(options);
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<UsuarioDto>(It.IsAny<Usuario>()))
            .Returns((Usuario usuario) => new UsuarioDto { Id = usuario.Id, NomUsuario = usuario.NomUsuario });
        var uow = new Mock<IUnitOfWorkAsync>();
        var service = new UsuarioService(
            new UsuarioRepository(context, uow.Object),
            uow.Object,
            mapper.Object,
            Mock.Of<IEmailQueue>(),
            Mock.Of<IConfigAppRepository>(),
            NullLogger<UsuarioService>.Instance);
        return (service, uow);
    }

    private static CrearUsuarioDto CrearDto() => new()
    {
        IdTenant = 1,
        IdEstado = 1,
        NomUsuario = "usuario-prueba",
        Email = "usuario-prueba@example.invalid",
        Nombre = "Usuario",
        Apellido = "Prueba"
    };
}
