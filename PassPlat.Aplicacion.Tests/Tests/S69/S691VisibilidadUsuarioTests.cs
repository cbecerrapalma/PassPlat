using CBP.Results;
using CBP.Results.Errors;
using Moq;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S69;

public class S691VisibilidadUsuarioTests
{
    [Fact]
    public async Task Uno_mismo_no_consulta_membresia()
    {
        var mock = new Mock<IUsuarioTenantRepository>();
        var service = new VisibilidadUsuarioService(mock.Object);

        var result = await service.VerificarAsync(1, 1, 1, "recurso", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        mock.Verify(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Miembro_activo_del_tenant_es_visible()
    {
        var mock = new Mock<IUsuarioTenantRepository>();
        mock.Setup(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(new UsuarioTenant { Id = 1, IdUsuario = 2, IdTenant = 1, Activo = true }, allowNull: true));

        var service = new VisibilidadUsuarioService(mock.Object);

        var result = await service.VerificarAsync(1, 2, 1, "recurso", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        mock.Verify(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_miembro_devuelve_FORBIDDEN()
    {
        var mock = new Mock<IUsuarioTenantRepository>();
        mock.Setup(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(null, allowNull: true));

        var service = new VisibilidadUsuarioService(mock.Object);

        var result = await service.VerificarAsync(1, 2, 1, "recurso", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.Error?.Code);
    }

    [Fact]
    public async Task Falla_de_BD_en_membresia_se_propaga()
    {
        var mock = new Mock<IUsuarioTenantRepository>();
        mock.Setup(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Failure("DB_ERROR", "error"));

        var service = new VisibilidadUsuarioService(mock.Object);

        var result = await service.VerificarAsync(1, 2, 1, "recurso", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("DB_ERROR", result.Error?.Code);
    }

    // S120 · D3 — revocar el MFA de otro: el propio siempre; otro sin permiso, FORBIDDEN sin consultar; con permiso, según la membresía
    [Theory]
    [InlineData(1, false, true, true, 0)]
    [InlineData(2, false, true, false, 0)]
    [InlineData(2, true, true, true, 1)]
    [InlineData(2, true, false, false, 1)]
    public async Task VerificarAdministracion_exige_permiso_y_membresia(int idUsuario, bool puedeAdministrar, bool esMiembro, bool esperado, int consultas)
    {
        var mock = new Mock<IUsuarioTenantRepository>();
        mock.Setup(r => r.ObtenerActivoPorTenantAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UsuarioTenant?>.Success(esMiembro ? new UsuarioTenant { Id = 1, IdUsuario = 2, IdTenant = 1, Activo = true } : null, allowNull: true));

        var service = new VisibilidadUsuarioService(mock.Object);

        var result = await service.VerificarAdministracionAsync(1, idUsuario, 1, puedeAdministrar, "MFA de otro usuario", TestContext.Current.CancellationToken);

        Assert.Equal(esperado, result.IsSuccess);
        if (!esperado) Assert.Equal("FORBIDDEN", result.Error?.Code);
        mock.Verify(r => r.ObtenerActivoPorTenantAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(consultas));
    }
}
