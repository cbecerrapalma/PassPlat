using Bunit;
using CBP.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using PassPlat.Web.Services;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>Base de las pruebas de la Web: servicios minimos y dobles.</summary>
public abstract class WebTestBase : Bunit.BunitContext
{
    protected FakeHttpHandler Handler { get; } = new();

    private readonly HttpClient _http;

    /// <summary>
    /// Se resuelve en diferido: bUnit no permite registrar servicios despues de pedir el primero.
    /// </summary>
    protected AuthService Auth => Services.GetRequiredService<AuthService>();

    protected ApiClient Api => Services.GetRequiredService<ApiClient>();

    protected WebTestBase()
    {
        _http = new HttpClient(Handler) { BaseAddress = new Uri("https://localhost:5001/") };
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddCbpBlazor();
        Services.AddSingleton(_http);
        Services.AddSingleton(sp => new AuthService(_http, sp.GetRequiredService<NavigationManager>(), sp.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(sp => new ApiClient(_http, sp.GetRequiredService<AuthService>()));
        Services.AddSingleton(sp => new MenuService(sp.GetRequiredService<ApiClient>(), sp.GetRequiredService<AuthService>()));
    }

    protected MenuService Menu => Services.GetRequiredService<MenuService>();

    /// <summary>Sesion de <c>sistema</c> en PLATFORM (tenant 1), sin red real.</summary>
    protected async Task AutenticarAsync(int idUsuario = 1, int idTenant = 1, string nomUsuario = "sistema", params string[] permisos)
    {
        var lista = permisos.Length > 0
            ? permisos
            : new[] { "ACCESOS_VER", "ACCESOS_ASIGNAR", "ACCESOS_REVOCAR", "USUARIOS_VER",
                // S114 · D5 — sistema (PLATFORM_ADMIN) tiene los FED_* en la BD; las páginas de Federación ahora los exigen
                "FED_PROVIDEN_VER", "FED_PROVIDEN_EDITAR", "FED_CONFPROVIDEN_VER", "FED_CONFPROVIDEN_EDITAR", "FED_IDENEXT_VER", "FED_IDENEXT_DESVINCULAR" };
        await Auth.RestoreSessionAsync(Token(idTenant, lista), null, idUsuario, idTenant, null, nomUsuario);
    }

    /// <summary>JWT de prueba: solo para que la Web lea el tenant y los permisos. No es una clave real.</summary>
    protected static string Token(int idTenant, string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("""{"alg":"HS256","typ":"JWT"}""");
        var payload = B64(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["TenantId"] = idTenant.ToString(),
            ["permiso"] = permisos
        }));
        return $"{header}.{payload}.firma-de-prueba";
    }

    protected void CargarCatalogos()
    {
        Handler.Responder("/api/apps", Datos.AppsJson());
        Handler.Responder("/api/roles", Datos.RolesJson());
        Handler.Responder("/api/tenants", Datos.TenantsJson());
        Handler.Responder("/api/usuarios", Datos.UsuariosJson());
    }
}
