using System.Text.Encodings.Web;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using Fluid;
using Microsoft.Extensions.Configuration;
using Moq;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S62;

/// <summary>
/// S62 D3: el texto que escribe el visitante (p. ej. el mensaje del contacto de Inventa Store)
/// nunca se evalúa como plantilla, aunque el cuerpo se vuelva a analizar al insertarlo en _layout.
/// </summary>
public class S62EmailApiInyeccionTests
{
    private const string Intento = "Hola {{ Variables.NombreTienda }} {% for i in (1..3) %}x{% endfor %}";

    // Mismo patrón que EmailTemplateStoreService.RenderBodyAsync: render del cuerpo y nuevo
    // análisis del resultado (paso del layout) con el mismo contexto.
    private static async Task<string> RenderDobleAsync(string cuerpo, Dictionary<string, object?> variables)
    {
        var parser = new FluidParser();
        var options = new TemplateOptions();
        var ctx1 = new TemplateContext(options);
        ctx1.SetValue("Variables", variables);
        var primero = await parser.Parse(cuerpo).RenderAsync(ctx1);
        var ctx2 = new TemplateContext(options);
        ctx2.SetValue("Variables", variables);
        return await parser.Parse(primero).RenderAsync(ctx2);
    }

    private static async Task<Dictionary<string, object?>> VariablesEncoladasAsync(Dictionary<string, string> entrada)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(x => x.HasTenant).Returns(true);
        tenant.Setup(x => x.CurrentId).Returns(3);
        var repo = new Mock<IEmailTemplateRepository>();
        repo.Setup(x => x.ObtenerPorNombreCulturaAsync("inventa-store-contacto", "es", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmailTemplate?>.Success(new EmailTemplate
            {
                Id = 40, Nombre = "inventa-store-contacto", Cultura = "es",
                Estado = "publicado", Categoria = "app-externa",
                Asunto = "Nuevo mensaje de contacto — {{ Variables.NombreTienda }}",
                CuerpoHtml = "<div>{{ Variables.Mensaje }}</div>"
            }));
        EmailJob? capturado = null;
        var cola = new Mock<IEmailQueue>();
        cola.Setup(x => x.EnqueueAsync(It.IsAny<EmailJob>(), It.IsAny<CancellationToken>()))
            .Callback<EmailJob, CancellationToken>((j, _) => capturado = j)
            .Returns(ValueTask.CompletedTask);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var sut = new EmailApiService(tenant.Object, repo.Object, cola.Object, cfg);

        var r = await sut.EnviarAsync(new EnviarEmailPlantillaRequest("inventa-store-contacto", "tienda@example.com")
        {
            Variables = entrada
        }, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.NotNull(capturado);
        return Assert.IsType<Dictionary<string, object?>>(capturado!.Extra["Variables"]);
    }

    [Fact]
    public async Task Llaves_del_visitante_se_neutralizan_al_encolar()
    {
        var vars = await VariablesEncoladasAsync(new() { ["NombreTienda"] = "Vestuario Demo", ["Mensaje"] = Intento });

        var mensaje = Assert.IsType<string>(vars["Mensaje"]);
        Assert.DoesNotContain("{", mensaje);
        Assert.DoesNotContain("}", mensaje);
        Assert.Contains("&#123;&#123;", mensaje);
        Assert.Equal("Vestuario Demo", vars["NombreTienda"]);
    }

    [Fact]
    public async Task Render_doble_muestra_el_texto_literal_y_el_nombre_de_la_BD()
    {
        var vars = await VariablesEncoladasAsync(new() { ["NombreTienda"] = "Vestuario Demo", ["Mensaje"] = Intento });

        var html = await RenderDobleAsync("<h1>{{ Variables.NombreTienda }}</h1><div>{{ Variables.Mensaje }}</div>", vars);

        // El nombre de la tienda (dato de la BD) sí se resuelve, y una sola vez.
        Assert.Equal("<h1>Vestuario Demo</h1>", html[..html.IndexOf("<div>")]);
        // Lo escrito por el visitante queda literal: ni la variable ni el bucle se evalúan.
        Assert.Contains("&#123;&#123; Variables.NombreTienda &#125;&#125;", html);
        Assert.Contains("&#123;% for i in (1..3) %&#125;x&#123;% endfor %&#125;", html);
        Assert.DoesNotContain("xxx", html);
    }

    [Fact]
    public async Task Sin_neutralizar_el_segundo_analisis_evaluaria_el_texto_del_visitante()
    {
        // Documenta el riesgo que cierra D3: solo HtmlEncoder (comportamiento anterior a S62).
        var vars = new Dictionary<string, object?>
        {
            ["NombreTienda"] = "Vestuario Demo",
            ["Mensaje"] = HtmlEncoder.Default.Encode(Intento)
        };

        var html = await RenderDobleAsync("<h1>{{ Variables.NombreTienda }}</h1><div>{{ Variables.Mensaje }}</div>", vars);

        Assert.Contains("xxx", html);
        Assert.Equal(2, html.Split("Vestuario Demo").Length - 1);
    }
}
