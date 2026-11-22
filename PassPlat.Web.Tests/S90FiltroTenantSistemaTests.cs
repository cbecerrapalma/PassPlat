using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Services;
using PassPlat.Web.Shared;
using GruposIndex = PassPlat.Web.Pages.Grupos.Index;
using UsuariosIndex = PassPlat.Web.Pages.Usuarios.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S90 · P50 — el filtro Tenant de /usuarios y /admin/grupos aparece para el usuario de sistema,
/// cuyo token real trae TenantId del tenant Plataforma y el claim is_system.
/// </summary>
public class S90FiltroTenantSistemaTests : WebTestBase
{
    private const string UsuariosPagina = """
    {
      "items": [
        { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "tenantNombre": "Plataforma", "apps": ["PASSPLAT"] },
        { "id": 5, "idTenant": 2, "nomUsuario": "admin_abarrotes", "email": "admin@abarrotes.test", "nombre": "Admin", "apellido": "Abarrotes", "eliminado": false, "tenantNombre": "Abarrotes del Sur", "apps": [] }
      ],
      "totalCount": 2,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private const string GruposJson = """
    [
      { "id": 1, "idTenant": 2, "codigo": "VENTAS", "nombre": "Ventas", "activo": true, "fecCrea": "2026-01-01T00:00:00", "tenantNombre": "Abarrotes del Sur" },
      { "id": 2, "idTenant": 3, "codigo": "BODEGA", "nombre": "Bodega", "activo": true, "fecCrea": "2026-01-01T00:00:00", "tenantNombre": "Vestuario SA de CV" }
    ]
    """;

    private readonly CapturaUrl _captura;

    public S90FiltroTenantSistemaTests()
    {
        // La base registra solo la ruta; aquí se guarda también la query para ver idTenant.
        _captura = new CapturaUrl(Handler);
        var http = new HttpClient(_captura) { BaseAddress = new Uri("https://localhost:5001/") };
        Services.AddSingleton(http);
        Services.AddSingleton(sp => new AuthService(http, sp.GetRequiredService<NavigationManager>(), sp.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(sp => new ApiClient(http, sp.GetRequiredService<AuthService>()));
        CargarCatalogos();
        Handler.Responder("/api/usuarios/page", UsuariosPagina);
        Handler.Responder("/api/grupos/stats", "{}");
        Handler.Responder("/api/grupos", GruposJson);
    }

    /// <summary>JWT como el real de sistema: TenantId del tenant Plataforma y is_system. No es una clave real.</summary>
    private static string TokenSistema(params string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("""{"alg":"HS256","typ":"JWT"}""");
        var payload = B64(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["TenantId"] = "1",
            ["is_system"] = "true",
            ["permiso"] = permisos
        }));
        return $"{header}.{payload}.firma-de-prueba";
    }

    private Task SesionSistemaAsync(params string[] permisos)
        => Auth.RestoreSessionAsync(TokenSistema(permisos), null, 1, 1, null, "sistema");

    [Fact]
    public async Task Usuarios_sistema_con_tenant_plataforma_ve_el_selector_de_tenant()
    {
        await SesionSistemaAsync("USUARIOS_VER", "APPS_VER");
        var cut = Render<UsuariosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("admin_abarrotes", cut.Markup));
        Assert.Single(cut.FindComponents<SelectorCatalogo<TenantDto>>());
        Assert.Contains(_captura.Urls, u => u.StartsWith("/api/tenants", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Usuarios_sistema_elegir_tenant_envia_idTenant_a_la_api()
    {
        await SesionSistemaAsync("USUARIOS_VER", "APPS_VER");
        var cut = Render<UsuariosIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<SelectorCatalogo<TenantDto>>().Single().Instance.Items));
        Assert.DoesNotContain(_captura.Urls, u => u.Contains("idTenant=", StringComparison.Ordinal));

        var selector = cut.FindComponents<SelectorCatalogo<TenantDto>>().Single();
        var tenant = selector.Instance.Items.First(t => t.Id == 2);
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(tenant));

        cut.WaitForAssertion(() => Assert.Contains(_captura.Urls,
            u => u.Contains("/api/usuarios/page", StringComparison.Ordinal) && u.Contains("&idTenant=2", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Usuarios_admin_de_plataforma_sin_is_system_no_ve_el_selector_de_tenant()
    {
        await AutenticarAsync(2, 1, "platform_admin", "USUARIOS_VER", "APPS_VER");
        var cut = Render<UsuariosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("admin_abarrotes", cut.Markup));
        Assert.Empty(cut.FindComponents<SelectorCatalogo<TenantDto>>());
        Assert.DoesNotContain(_captura.Urls, u => u.StartsWith("/api/tenants", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Grupos_sistema_ve_el_filtro_tenant()
    {
        await SesionSistemaAsync("GRUPOS_VER");
        var cut = Render<GruposIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Ventas", cut.Markup));
        Assert.Contains(cut.FindComponents<MudSelect<string>>(), s => s.Instance.Label == "Tenant");
    }

    [Fact]
    public async Task Grupos_admin_de_tenant_no_ve_el_filtro_tenant()
    {
        await AutenticarAsync(5, 2, "admin_abarrotes", "GRUPOS_VER");
        var cut = Render<GruposIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Ventas", cut.Markup));
        Assert.DoesNotContain(cut.FindComponents<MudSelect<string>>(), s => s.Instance.Label == "Tenant");
    }

    private sealed class CapturaUrl(HttpMessageHandler interno) : DelegatingHandler(interno)
    {
        private readonly ConcurrentQueue<string> _urls = new();

        public IReadOnlyList<string> Urls => _urls.ToArray();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _urls.Enqueue(request.RequestUri?.PathAndQuery ?? "");
            return base.SendAsync(request, ct);
        }
    }
}
