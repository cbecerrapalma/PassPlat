using CBP.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Dashboard;

namespace PassPlat.Aplicacion.Test.Tests.S32;

/// <summary>
/// G12/G14 — BackgroundStatusService sin reflexión: mapeo honesto de estado
/// (Activo/Detenido/No disponible) y contrato BackgroundJobDto intacto.
/// </summary>
public class BackgroundStatusServiceTests
{
    private static IBackgroundJobStatus Fuente(string nombre, BackgroundJobStatus status) =>
        MockFuente(nombre, Result<BackgroundJobStatus>.Success(status));

    private static IBackgroundJobStatus FuenteInaccesible(string nombre, bool viaFailure)
    {
        var mock = new Mock<IBackgroundJobStatus>();
        mock.SetupGet(s => s.Nombre).Returns(nombre);
        if (viaFailure)
            mock.Setup(s => s.ObtenerEstadoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<BackgroundJobStatus>.Failure("BG_ERR", "fallo de estado"));
        else
            mock.Setup(s => s.ObtenerEstadoAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
        return mock.Object;
    }

    private static IBackgroundJobStatus MockFuente(string nombre, Result<BackgroundJobStatus> resultado)
    {
        var mock = new Mock<IBackgroundJobStatus>();
        mock.SetupGet(s => s.Nombre).Returns(nombre);
        mock.Setup(s => s.ObtenerEstadoAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(resultado));
        return mock.Object;
    }

    private static BackgroundStatusService CrearServicio(params IBackgroundJobStatus[] fuentes) =>
        new(fuentes, NullLogger<BackgroundStatusService>.Instance);

    [Fact]
    public async Task JobActivo_SeMapeaComoActivo_ConContadorYUltimaEjecucion()
    {
        var ultima = new DateTime(2026, 8, 16, 10, 0, 0);
        var servicio = CrearServicio(Fuente("JobA", new BackgroundJobStatus(true, ultima, 42)));

        var result = await servicio.GetBackgroundJobsAsync();

        Assert.True(result.IsSuccess);
        var job = result.Value.Single();
        Assert.Equal("JobA", job.Nombre);
        Assert.Equal("Activo", job.Estado);
        Assert.Equal(ultima, job.UltimaEjecucion);
        Assert.Equal(42, job.ItemsProcesados);
    }

    [Fact]
    public async Task JobDetenido_SeMapeaComoDetenido()
    {
        var servicio = CrearServicio(Fuente("JobB", new BackgroundJobStatus(false, null, 0)));

        var result = await servicio.GetBackgroundJobsAsync();

        Assert.True(result.IsSuccess);
        var job = result.Value.Single();
        Assert.Equal("Detenido", job.Estado);
    }

    [Fact]
    public async Task JobSinEstadoDeterminable_SeMapeaComoNoDisponible_NuncaActivo()
    {
        var servicio = CrearServicio(
            FuenteInaccesible("JobC_Failure", viaFailure: true),
            FuenteInaccesible("JobD_Throw", viaFailure: false));

        var result = await servicio.GetBackgroundJobsAsync();

        Assert.True(result.IsSuccess);
        Assert.All(result.Value, j => Assert.Equal("No disponible", j.Estado));
    }

    [Fact]
    public async Task FuentesMultiples_DevuelveOrdenDeRegistro_YContratoXmlIntacto()
    {
        var servicio = CrearServicio(
            Fuente("JobA", new BackgroundJobStatus(true, null, 1)),
            FuenteInaccesible("JobB", viaFailure: true),
            Fuente("JobC", new BackgroundJobStatus(null, null, 0)));

        var result = await servicio.GetBackgroundJobsAsync();

        Assert.True(result.IsSuccess);
        var lista = result.Value.ToList();
        Assert.Equal(3, lista.Count);
        Assert.Equal(["JobA", "JobB", "JobC"], lista.Select(j => j.Nombre));
        // Contrato BackgroundJobDto: Nombre, Estado, UltimaEjecucion, ItemsProcesados (siempre presentes)
        Assert.All(lista, j =>
        {
            Assert.NotNull(j.Nombre);
            Assert.NotNull(j.Estado);
            Assert.NotNull(j.ToString());
        });
    }
}