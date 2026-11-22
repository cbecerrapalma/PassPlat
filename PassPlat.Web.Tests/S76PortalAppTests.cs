using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Models;
using PassPlat.Web.Services;
using Login = PassPlat.Web.Pages.Login;

namespace PassPlat.Web.Tests;

/// <summary>
/// S76 — el portal AccessPlat entra solo con su app (AppSettings.AppId): el login no ofrece
/// elegir aplicacion y una sesion guardada de otra app se descarta.
/// </summary>
public class S76PortalAppTests : WebTestBase
{
    public S76PortalAppTests()
    {
        Services.AddSingleton(new AppSettings { AppId = 1 });
        Handler.Responder("/api/auth/tenant-info", """{ "requiereSeleccion": false, "idTenant": 1, "nombre": "Plataforma" }""");
        Handler.Responder("/api/auth/tenants", """[{ "id": 1, "nombre": "Plataforma", "codigo": "PLAT" }]""");
        Handler.ResponderConError("/api/auth/login", HttpStatusCode.Forbidden,
            """{ "codigo": "SIN_ACCESO_APP", "mensaje": "x [SIN_ACCESO_APP]" }""");
    }

    private AuthService NuevoAuth(AppSettings? appSettings) =>
        new(new HttpClient(Handler) { BaseAddress = new Uri("https://localhost:5001/") },
            Services.GetRequiredService<NavigationManager>(), JSInterop.JSRuntime, appSettings);

    /// <summary>JWT de prueba con el claim IdApp. No es una clave real.</summary>
    private static string TokenDeApp(string idApp)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("""{"alg":"HS256","typ":"JWT"}""");
        var payload = B64(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["TenantId"] = "1",
            ["permiso"] = new[] { "USUARIOS_VER" },
            ["IdApp"] = idApp
        }));
        return $"{header}.{payload}.firma-de-prueba";
    }

    private IRenderedComponent<Login> RenderLogin()
    {
        var cut = Render<Login>();
        cut.WaitForAssertion(() => Assert.Contains("Iniciar Sesión", cut.Markup), TimeSpan.FromSeconds(15));
        return cut;
    }

    [Fact]
    public void AuthService_ConAppSettings_UsaSuAppId()
    {
        Assert.Equal(7, NuevoAuth(new AppSettings { AppId = 7 }).AppId);
    }

    [Fact]
    public void AuthService_SinAppSettings_UsaApp1()
    {
        Assert.Equal(1, NuevoAuth(null).AppId);
    }

    [Fact]
    public void Login_NoMuestraSelectorDeAplicacion_NiPideApps()
    {
        var cut = RenderLogin();

        Assert.DoesNotContain("Selecciona una aplicación", cut.Markup);
        Assert.DoesNotContain(Handler.Peticiones, p => p.EndsWith("/api/apps/activas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_SinAccesoApp_MuestraMensajeDelPortal()
    {
        var cut = RenderLogin();

        await cut.InvokeAsync(() =>
        {
            var campos = cut.FindAll("input").Where(i => i.GetAttribute("type") == "text" && i.ParentElement?.ParentElement?.ClassName?.Contains("mud-select") != true).ToList();
            campos[^1].Input("invitado");
            cut.FindAll("input").First(i => i.GetAttribute("type") == "password").Change("clave-de-prueba");
        });

        await cut.InvokeAsync(() =>
        {
            var boton = cut.FindAll("button").First(b => b.TextContent.Contains("Iniciar Sesión", StringComparison.OrdinalIgnoreCase));
            boton.Click();
        });

        cut.WaitForAssertion(() => Assert.Contains("no tiene acceso a AccessPlat", cut.Markup), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain("[SIN_ACCESO_APP]", cut.Markup);
    }

    [Fact]
    public async Task RestoreSession_TokenDeOtraApp_DescartaLaSesion()
    {
        var auth = NuevoAuth(new AppSettings { AppId = 1 });

        await auth.RestoreSessionAsync(TokenDeApp("13"), null, 5, 1, null, "invitado");

        Assert.False(auth.IsAuthenticated);
        Assert.Null(auth.AccessToken);
    }

    [Fact]
    public async Task RestoreSession_TokenDeLaAppDelPortal_ConservaLaSesion()
    {
        var auth = NuevoAuth(new AppSettings { AppId = 1 });

        await auth.RestoreSessionAsync(TokenDeApp("1"), null, 5, 1, null, "admin");

        Assert.True(auth.IsAuthenticated);
    }
}
