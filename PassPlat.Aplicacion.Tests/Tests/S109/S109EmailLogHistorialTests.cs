using AutoMapper;
using CBP.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Mapping;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S109;

/// <summary>
/// S109 · D6 — el historial de envíos se pide al repositorio con los filtros y el tope, y el
/// servicio acota al tenant de la sesión ajustando el tope al rango 1..500.
/// </summary>
public class S109EmailLogHistorialTests
{
    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<AplicacionProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ObtenerRecientes_con_alcance_de_tenant_excluye_otros_tenants_y_sin_tenant()
    {
        var repo = new Mock<IEmailLogRepository>();
        var filas = new List<EmailLog>
        {
            EmailLog.Crear("ana@a.test", "Uno", idTenant: 2),
            EmailLog.Crear("beto@b.test", "Dos", idTenant: 3),
            EmailLog.Crear("sin@tenant.test", "Tres")
        };
        repo.Setup(r => r.ObtenerRecientesAsync(2, null, null, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<EmailLog>>.Success(filas));
        var servicio = new EmailLogService(repo.Object, Mapper);

        var result = await servicio.ObtenerRecientesAsync(2, null, null, 200, Ct);

        Assert.True(result.IsSuccess);
        var fila = Assert.Single(result.Value!);
        Assert.Equal(2, fila.IdTenant);
        Assert.Equal("ana@a.test", fila.Destinatario);
    }

    [Fact]
    public async Task ObtenerRecientes_ajusta_top_a_1_500_y_filtra_por_estado()
    {
        var repo = new Mock<IEmailLogRepository>();
        var filas = new List<EmailLog> { EmailLog.Crear("error@a.test", "Falla", idTenant: 2) };
        repo.Setup(r => r.ObtenerRecientesAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<EmailLog>>.Success(filas));
        var servicio = new EmailLogService(repo.Object, Mapper);

        var minimo = await servicio.ObtenerRecientesAsync(2, "error", 7, 0, Ct);
        var maximo = await servicio.ObtenerRecientesAsync(2, "error", 7, 9999, Ct);

        repo.Verify(r => r.ObtenerRecientesAsync(2, "error", 7, 1, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.ObtenerRecientesAsync(2, "error", 7, 500, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(minimo.IsSuccess);
        Assert.True(maximo.IsSuccess);
        Assert.Single(minimo.Value!);
        Assert.Single(maximo.Value!);
    }
}
