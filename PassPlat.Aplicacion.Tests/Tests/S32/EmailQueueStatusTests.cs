using CBP.Logging.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Tests.Tests.Framework.S17;

namespace PassPlat.Aplicacion.Tests.Tests.S32;

/// <summary>
/// G13 — EmailQueue como fuente de estado operacional del Dashboard:
/// documenta explícitamente que NO es un BackgroundService.
/// </summary>
public class EmailQueueStatusTests
{
    private static EmailQueue CrearCola()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>().Object;
        return new EmailQueue(new CapturingLoggerService(), httpContextAccessor);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_ReportaColaActiva_ConConteoPendiente()
    {
        var cola = CrearCola();
        await cola.EnqueueAsync(new EmailJob(EmailJobKind.Welcome, "a@b.com", "User", new Dictionary<string, object?>()), TestContext.Current.CancellationToken);
        await cola.EnqueueAsync(new EmailJob(EmailJobKind.Welcome, "c@d.com", "Other", new Dictionary<string, object?>()), TestContext.Current.CancellationToken);

        var result = await cola.ObtenerEstadoAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var estado = result.Value;
        Assert.Equal("EmailQueue", ((IBackgroundJobStatus)cola).Nombre);
        Assert.True(estado.Ejecutando);
        Assert.Null(estado.UltimaEjecucion);
        Assert.Equal(2, estado.ItemsProcesados);
        Assert.Contains("no es BackgroundService", estado.Detalle);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_ConColaVacia_ReportaCeroPendientes()
    {
        var cola = CrearCola();

        var result = await cola.ObtenerEstadoAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ItemsProcesados);
        Assert.True(result.Value.Ejecutando);
    }
}
