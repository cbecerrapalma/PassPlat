using CBP.Caching.Interfaces;
using CBP.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S62;

/// <summary>
/// S62.1: el asunto y el texto plano no son HTML. Las variables de la API pública llegan
/// codificadas ("&amp;", "&#123;", "&#xA;") y deben verse decodificadas, sin evaluarse.
/// Usa EmailTemplateStoreService y Fluid reales; solo el repositorio y la caché son simulados.
/// </summary>
public class S621TextoPlanoTests
{
    private static EmailTemplateStoreService Sut(EmailTemplate plantilla)
    {
        var repo = new Mock<IEmailTemplateRepository>();
        repo.Setup(x => x.ObtenerPorNombreCulturaAsync(plantilla.Nombre, "es", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(plantilla));
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IEmailTemplateRepository))).Returns(repo.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);
        var cache = new Mock<ICacheService>(); // GetAsync devuelve null: siempre compila
        return new EmailTemplateStoreService(factory.Object, cache.Object, NullLogger<EmailTemplateStoreService>.Instance);
    }

    private static EmailTemplate Plantilla(string? cuerpoTexto) => new()
    {
        Id = 40, Nombre = "inventa-store-contacto", Cultura = "es", Estado = "publicado", Categoria = "app-externa",
        Asunto = "Nuevo mensaje de contacto — {{ Variables.NombreTienda }}",
        CuerpoHtml = "<div>{{ Variables.Mensaje }}</div>",
        CuerpoTexto = cuerpoTexto
    };

    private static Dictionary<string, object?> Vars(string nombreTienda, string mensaje) => new()
    {
        ["Variables"] = new Dictionary<string, object?> { ["NombreTienda"] = nombreTienda, ["Mensaje"] = mensaje }
    };

    [Fact]
    public async Task Asunto_decodifica_entidades_y_quita_saltos_de_linea()
    {
        var r = await Sut(Plantilla(null)).RenderSubjectAsync("inventa-store-contacto",
            Vars("Ropa &amp; Más&#xA;Demo", "x"), ct: TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Equal("Nuevo mensaje de contacto — Ropa & Más Demo", r.Value);
    }

    [Fact]
    public async Task Texto_plano_null_si_la_plantilla_no_lo_tiene()
    {
        var r = await Sut(Plantilla(null)).RenderTextAsync("inventa-store-contacto",
            Vars("Vestuario Demo", "x"), ct: TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task Texto_plano_decodifica_y_deja_literal_lo_del_visitante()
    {
        var r = await Sut(Plantilla("Tienda {{ Variables.NombreTienda }}\nMensaje:\n{{ Variables.Mensaje }}"))
            .RenderTextAsync("inventa-store-contacto",
                Vars("Ropa &amp; Más", "L1&#xA;&#123;&#123; Variables.NombreTienda &#125;&#125; &amp; fin"),
                ct: TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Equal("Tienda Ropa & Más\nMensaje:\nL1\n{{ Variables.NombreTienda }} & fin", r.Value);
    }
}
