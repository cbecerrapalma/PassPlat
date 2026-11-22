using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using Login = PassPlat.Web.Pages.Login;

namespace PassPlat.Web.Tests;

/// <summary>
/// S59 · K6 (D9) — el login no puede quedarse en «Iniciando sesión…» cuando el servidor
/// responde con un error de validacion (400): el boton tiene que volver a habilitarse y
/// hay que enseñar el mensaje del servidor.
/// </summary>
public class S59LoginNoColgadoTests : WebTestBase
{
    public S59LoginNoColgadoTests()
    {
        Services.AddSingleton<PassPlat.Web.Models.AppSettings>(new PassPlat.Web.Models.AppSettings { AppId = 1 });
        Handler.Responder("/api/auth/tenant-info", """{ "requiereSeleccion": false, "idTenant": 1, "nombre": "Plataforma" }""");
        Handler.Responder("/api/auth/tenants", """[{ "id": 1, "nombre": "Plataforma", "codigo": "PLAT" }]""");
        // El servidor rechaza el inicio de sesion con un 400 de validacion.
        Handler.ResponderConError("/api/auth/login", HttpStatusCode.BadRequest,
            """{ "codigo": "CREDENCIALES_INVALIDAS", "mensaje": "Usuario o contrasena incorrectos" }""");
    }

    private async Task<IRenderedComponent<Login>> RenderLoginAsync()
    {
        var cut = Render<Login>();
        cut.WaitForAssertion(() => Assert.Contains("Iniciar Sesión", cut.Markup), TimeSpan.FromSeconds(15));
        return cut;
    }

    [Fact]
    public async Task Un_400_del_servidor_vuelve_a_habilitar_el_boton_y_muestra_el_mensaje()
    {
        var cut = await RenderLoginAsync();

        // Los elementos se buscan justo antes de usarlos: la pagina se redibuja al
        // resolver aplicacion y tenant y las referencias viejas dejan de valer.
        await cut.InvokeAsync(() =>
        {
            var campos = cut.FindAll("input").Where(i => i.GetAttribute("type") == "text" && i.ParentElement?.ParentElement?.ClassName?.Contains("mud-select") != true).ToList();
            campos[^1].Input("sistema");
            cut.FindAll("input").First(i => i.GetAttribute("type") == "password").Change("clave-erronea");
        });

        await cut.InvokeAsync(() =>
        {
            var boton = cut.FindAll("button").First(b => b.TextContent.Contains("Iniciar Sesión", StringComparison.OrdinalIgnoreCase));
            boton.Click();
        });

        // El boton vuelve a estar disponible (no se queda colgado en «Iniciando sesion...»).
        cut.WaitForAssertion(() =>
        {
            var b = cut.FindAll("button").First(x => x.TextContent.Contains("Iniciar Sesión", StringComparison.OrdinalIgnoreCase));
            Assert.False(b.HasAttribute("disabled"));
        }, TimeSpan.FromSeconds(15));

        // Y se muestra el mensaje del servidor.
        Assert.Contains(Handler.Peticiones, p => p.Contains("POST /api/auth/login"));
        cut.WaitForAssertion(() => Assert.Contains("Usuario o contrasena incorrectos", cut.Markup), TimeSpan.FromSeconds(15));
    }
}
