using System.Text.Encodings.Web;
using Microsoft.Extensions.Configuration;
using Moq;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Tests.Tests.Framework.S17;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S78;

public class EmailApiServiceTests
{
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<IEmailTemplateRepository> _templateRepoMock = new();
    private readonly Mock<IEmailQueue> _emailQueueMock = new();
    private readonly IConfiguration _config;
    private readonly CapturingLoggerService _logger = new();
    private readonly EmailApiService _service;

    public EmailApiServiceTests()
    {
        // Create real configuration with test values
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailApi:CategoriasPermitidas:0"] = "app-externa"
            });
        _config = configBuilder.Build();

        _service = new EmailApiService(
            _tenantContextMock.Object,
            _templateRepoMock.Object,
            _emailQueueMock.Object,
            _config);
    }

    [Fact]
    public async Task EnviarAsync_NoTenant_ReturnsForbidden()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(false);

        var request = new EnviarEmailPlantillaRequest("test-template", "test@example.com");

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("TENANT_REQUIRED", result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task EnviarAsync_TemplateNotFound_ReturnsNotFound()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("no-existe", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(null, allowNull: true));

        var request = new EnviarEmailPlantillaRequest("no-existe", "test@example.com");

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_PLANTILLA_NO_ENCONTRADA", result.Error!.Code);
        Assert.Equal(404, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task EnviarAsync_TemplateNotPublished_ReturnsNotFound()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("borrador-template", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 1,
                Nombre = "borrador-template",
                Cultura = "es",
                Estado = "borrador",
                Categoria = "app-externa"
            }));

        var request = new EnviarEmailPlantillaRequest("borrador-template", "test@example.com");

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_PLANTILLA_NO_PUBLICADA", result.Error!.Code);
        Assert.Equal(404, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task EnviarAsync_CategoryNotAllowed_ReturnsForbidden()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("reset-template", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 1,
                Nombre = "reset-template",
                Cultura = "es",
                Estado = "publicado",
                Categoria = "reset-password"
            }));

        var request = new EnviarEmailPlantillaRequest("reset-template", "test@example.com");

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_PLANTILLA_NO_PERMITIDA", result.Error!.Code);
        Assert.Equal(403, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task EnviarAsync_Success_ReturnsAccepted()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("test-template", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 42,
                Nombre = "test-template",
                Cultura = "es",
                Estado = "publicado",
                Categoria = "app-externa",
                Asunto = "Test {{Nombre}}",
                CuerpoHtml = "<p>Hola {{Nombre}}</p>"
            }));

        _emailQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<EmailJob>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var request = new EnviarEmailPlantillaRequest("test-template", "test@example.com")
        {
            NombreDestinatario = "Juan",
            Variables = new Dictionary<string, string> { ["Nombre"] = "Juan" }
        };

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.IdEmailLog);
        Assert.NotNull(result.Value.TrackingId);
        _emailQueueMock.Verify(x => x.EnqueueAsync(It.Is<EmailJob>(j =>
            j.Kind == EmailJobKind.ApiPlantilla &&
            j.ToEmail == "test@example.com" &&
            j.UserName == "Juan" &&
            j.IdTenant == 3 &&
            j.IdApp == null &&
            j.Extra.ContainsKey("TemplateCode") &&
            j.Extra["TemplateCode"]!.ToString() == "test-template"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_HtmlEncoding_ValuesAreEncoded()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("test-template", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 42,
                Nombre = "test-template",
                Cultura = "es",
                Estado = "publicado",
                Categoria = "app-externa",
                Asunto = "Test {{Nombre}}",
                CuerpoHtml = "<p>Hola {{Nombre}}</p>"
            }));

        _emailQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<EmailJob>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var request = new EnviarEmailPlantillaRequest("test-template", "test@example.com")
        {
            Variables = new Dictionary<string, string> { ["Nombre"] = "<script>alert('xss')</script>" }
        };

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        _emailQueueMock.Verify(x => x.EnqueueAsync(It.Is<EmailJob>(j =>
            j.Extra.ContainsKey("Variables") &&
            j.Extra["Variables"] is Dictionary<string, object?> &&
            ((Dictionary<string, object?>)j.Extra["Variables"]!).ContainsKey("Nombre") &&
            ((Dictionary<string, object?>)j.Extra["Variables"]!)["Nombre"] != null &&
            // Valores HTML-codificados: < -> <, > -> >
            // El valor codificado NO debe contener etiquetas HTML crudas
            !((Dictionary<string, object?>)j.Extra["Variables"]!)["Nombre"]!.ToString()!.Contains("<script>") &&
            !((Dictionary<string, object?>)j.Extra["Variables"]!)["Nombre"]!.ToString()!.Contains("</script>")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_QueueThrows_ReturnsExternalServiceError()
    {
        _tenantContextMock.Setup(x => x.HasTenant).Returns(true);
        _tenantContextMock.Setup(x => x.CurrentId).Returns(3);

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync("test-template", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 42,
                Nombre = "test-template",
                Cultura = "es",
                Estado = "publicado",
                Categoria = "app-externa",
                Asunto = "Test",
                CuerpoHtml = "<p>Test</p>"
            }));

        _emailQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<EmailJob>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Queue unavailable"));

        var request = new EnviarEmailPlantillaRequest("test-template", "test@example.com");

        var result = await _service.EnviarAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("EMAIL_QUEUE_ERROR", result.Error!.Code);
        Assert.Equal(503, result.Error.HttpStatusCode);
        Assert.Equal(ErrorType.ExternalService, result.Error.Type);
    }
}
