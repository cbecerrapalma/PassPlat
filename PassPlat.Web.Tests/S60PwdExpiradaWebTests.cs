using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Models;
using PassPlat.Web.Pages;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

// S60 · D5 — el login rechazado por contraseña vencida (403 PWD_EXPIRADA) muestra el
// mensaje del plan y el enlace para restablecer la contraseña.
public class S60PwdExpiradaWebTests : WebTestBase
{
    private const string Mensaje = "Tu contraseña venció. Restablécela con «¿Olvidaste tu contraseña?» o pide a un administrador que la cambie.";

    public S60PwdExpiradaWebTests()
    {
        Services.AddSingleton(new AppSettings { AppId = 1 });
        Handler.Responder("/api/auth/tenant-info", """{ "requiereSeleccion": false, "idTenant": 1, "nombre": "Plataforma" }""");
        Handler.Responder("/api/auth/tenants", """[{ "id": 1, "nombre": "Plataforma", "codigo": "PLAT" }]""");
        Handler.ResponderConError("/api/auth/login", HttpStatusCode.Forbidden,
            $$"""{ "codigo": "PWD_EXPIRADA", "mensaje": "{{Mensaje}}" }""");
    }

    private async Task<IRenderedComponent<Login>> RenderLoginAsync()
    {
        var cut = Render<Login>();
        cut.WaitForAssertion(() => Assert.Contains("Iniciar Sesión", cut.Markup), TimeSpan.FromSeconds(15));
        return cut;
    }

    [Fact]
    public async Task Un_403_de_contrasena_vencida_muestra_el_mensaje_y_el_enlace_de_restablecimiento()
    {
        var cut = await RenderLoginAsync();

        await cut.InvokeAsync(() =>
        {
            var campos = cut.FindAll("input")
                .Where(i => i.GetAttribute("type") == "text" && i.ParentElement?.ParentElement?.ClassName?.Contains("mud-select") != true)
                .ToList();
            campos[^1].Input("admin_vestuario");
            cut.FindAll("input").First(i => i.GetAttribute("type") == "password").Change("clave-vencida");
        });

        await cut.InvokeAsync(() =>
        {
            var boton = cut.FindAll("button").First(b => b.TextContent.Contains("Iniciar Sesión", StringComparison.OrdinalIgnoreCase));
            boton.Click();
        });

        cut.WaitForAssertion(() => Assert.Contains("Tu contraseña venció", cut.Markup), TimeSpan.FromSeconds(15));

        // El enlace al restablecimiento solo aparece con el código PWD_EXPIRADA.
        Assert.Contains("Restablecer mi contraseña", cut.Markup);
        Assert.Contains(Handler.Peticiones, p => p.Contains("POST /api/auth/login"));
    }

    [Fact]
    public async Task Un_401_de_credenciales_no_muestra_el_enlace_de_restablecimiento()
    {
        Handler.ResponderConError("/api/auth/login", HttpStatusCode.Unauthorized,
            """{ "codigo": "CREDENCIALES_INVALIDAS", "mensaje": "Usuario o contrasena incorrectos" }""");

        var cut = await RenderLoginAsync();

        await cut.InvokeAsync(() =>
        {
            var campos = cut.FindAll("input")
                .Where(i => i.GetAttribute("type") == "text" && i.ParentElement?.ParentElement?.ClassName?.Contains("mud-select") != true)
                .ToList();
            campos[^1].Input("admin_vestuario");
            cut.FindAll("input").First(i => i.GetAttribute("type") == "password").Change("clave-erronea");
        });

        await cut.InvokeAsync(() =>
        {
            var boton = cut.FindAll("button").First(b => b.TextContent.Contains("Iniciar Sesión", StringComparison.OrdinalIgnoreCase));
            boton.Click();
        });

        cut.WaitForAssertion(() => Assert.Contains("Usuario o contrasena incorrectos", cut.Markup), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain("Restablecer mi contraseña", cut.Markup);
    }
}
