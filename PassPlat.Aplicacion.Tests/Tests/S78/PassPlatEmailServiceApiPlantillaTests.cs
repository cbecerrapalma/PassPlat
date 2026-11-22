using System.Text.Json;
using Moq;
using CBP.Data.Abstractions;
using CBP.Emails.Configuration;
using CBP.Emails.Core.Models;
using CBP.Emails.Services;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Tests.Tests.Framework.S17;

namespace PassPlat.Aplicacion.Tests.Tests.S78;

public class PassPlatEmailServiceApiPlantillaTests
{
    private readonly Mock<IEmailAccountResolverService> _accountResolverMock = new();
    private readonly Mock<IEmailLogRepository> _emailLogRepoMock = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<IEmailTemplateRepository> _templateRepoMock = new();
    private readonly Mock<IUnitOfWorkAsync> _uowMock = new();
    private readonly Mock<IEmailTemplateStoreService> _templateStoreMock = new();
    private readonly Mock<ILogger<PassPlatEmailService>> _loggerMock = new();
    private readonly CapturingLoggerService _olog = new();
    private readonly PassPlatEmailService _service;

    public PassPlatEmailServiceApiPlantillaTests()
    {
        // Setup RenderSubjectAsync - service calls with 3 args using named param for ct (5th param)
        // Signature: (templateCode, variables, cultura = "es", idTenant = null, ct = default)
        _templateStoreMock.Setup(x => x.RenderSubjectAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<string>(),      // cultura
            It.IsAny<int?>(),        // idTenant
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Test Subject"));
        
        // Setup RenderBodyAsync - same signature
        _templateStoreMock.Setup(x => x.RenderBodyAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<string>(),      // cultura
            It.IsAny<int?>(),        // idTenant
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("<p>Test Body</p>"));

        _service = new PassPlatEmailService(
            _accountResolverMock.Object,
            _emailLogRepoMock.Object,
            _usuarioRepoMock.Object,
            _templateRepoMock.Object,
            _uowMock.Object,
            _templateStoreMock.Object,
            _loggerMock.Object,
            _olog);
    }

    [Fact]
    public async Task SendFromTemplateWithJobAsync_ApiPlantilla_UsesTemplateCodeFromExtra()
    {
        // Arrange
        var job = new EmailJob(
            Kind: EmailJobKind.ApiPlantilla,
            ToEmail: "test@example.com",
            UserName: "Test User",
            Extra: new Dictionary<string, object?>
            {
                ["TemplateCode"] = "inventa-store-contacto",
                ["Variables"] = new Dictionary<string, object?> { ["NombreTienda"] = "Mi Tienda" }
            },
            IdTenant: 3,
            IdUsuario: null,
            IdApp: null,
            CorrelationId: "test-correlation");

        _templateRepoMock.Setup(x => x.ObtenerPorNombreCulturaAsync(
            It.Is<string>(s => s == "inventa-store-contacto"),
            It.Is<string>(s => s == "es"),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PassPlat.Dominio.Entities.Core.EmailTemplate?>.Success(new PassPlat.Dominio.Entities.Core.EmailTemplate
            {
                Id = 42,
                Nombre = "inventa-store-contacto",
                Cultura = "es",
                Estado = "publicado",
                Categoria = "app-externa"
            }));

        var account = new PassPlat.Dominio.Entities.Core.EmailAccount
        {
            Id = 1,
            Nombre = "Test",
            Host = "smtp.test",
            Puerto = 587,
            SmtpUsuario = "user@test.com",
            Password = "pass",
            FromAddress = "from@test.com",
            FromName = "Test",
            UsaSSL = true,
            UsaTLS = true,
            Activo = true,
            Provider = new PassPlat.Dominio.Entities.Catalogos.EmailProvider { Id = 1, Nombre = "SMTP", Codigo = "SMTP" }
        };
        var smtpConfig = new SmtpAccountConfig
        {
            Host = "smtp.test",
            Port = 587,
            Username = "user@test.com",
            Password = "pass",
            FromEmail = "from@test.com",
            FromName = "Test"
        };
        _accountResolverMock.Setup(x => x.ResolveAsync(
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(PassPlat.Dominio.Entities.Core.EmailAccount, SmtpAccountConfig)>.Success((account, smtpConfig)));

        _emailLogRepoMock.Setup(x => x.Add(It.IsAny<PassPlat.Dominio.Entities.Core.EmailLog>())).Returns((PassPlat.Dominio.Entities.Core.EmailLog e) => e);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _service.SendFromJobAsync(job, TestContext.Current.CancellationToken);

        // Assert
        // El test verifica que la lógica de obtención de plantilla funciona correctamente.
        // El envío real falla porque no hay servidor SMTP real, pero la lógica previa debe ser correcta.
        // Verificar que NO devolvió UNKNOWN_KIND (significa que templateCode se resolvió bien)
        Assert.NotEqual("UNKNOWN_KIND", result.Error?.Code);
        // Verificar que se buscó la plantilla correcta desde Extra
        _templateRepoMock.Verify(x => x.ObtenerPorNombreCulturaAsync(
            It.Is<string>(s => s == "inventa-store-contacto"),
            It.Is<string>(s => s == "es"),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Verificar que llegó al punto de enviar (el error es de SMTP, no de lógica de negocio)
        // EMAIL_SEND_ERROR indica que la plantilla se encontró y se intentó enviar
        Assert.Contains(result.Error?.Code, new[] { "EMAIL_SEND_ERROR", "EMAIL_PROVIDER_ERROR" });
    }

    [Fact]
    public async Task SendFromTemplateWithJobAsync_ApiPlantilla_NoTemplateCodeInExtra_ReturnsFailure()
    {
        // Arrange
        var job = new EmailJob(
            Kind: EmailJobKind.ApiPlantilla,
            ToEmail: "test@example.com",
            UserName: "Test User",
            Extra: new Dictionary<string, object?> // sin TemplateCode
            {
                ["Variables"] = new Dictionary<string, object?>()
            },
            IdTenant: 3,
            IdUsuario: null,
            IdApp: null,
            CorrelationId: "test-correlation");

        // Act
        var result = await _service.SendFromJobAsync(job, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("UNKNOWN_KIND", result.Error!.Code);
    }
}
