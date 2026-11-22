using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Services;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;
using AccesosIndex = PassPlat.Web.Pages.Accesos.Index;
using RolesIndex = PassPlat.Web.Pages.Roles.Index;
using TenantsIndex = PassPlat.Web.Pages.Tenants.Index;
using UsuariosIndex = PassPlat.Web.Pages.Usuarios.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S85 · D1–D6 — cada lista tiene su barra horizontal de filtros (<c>table-toolbar filter-row</c>
/// o <c>CrudToolbar</c>) sin campos a todo el ancho, y <c>/usuarios</c> muestra Tenant y Apps y
/// filtra por tenant/app en el servidor.
/// </summary>
public class S85FiltrosListasTests : WebTestBase
{
    private static readonly string[] Listas =
    [
        "PassPlat.Web/Pages/Usuarios/Index.razor",
        "PassPlat.Web/Pages/Tenants/Index.razor",
        "PassPlat.Web/Pages/Apps/Index.razor",
        "PassPlat.Web/Pages/HistorialPwd/Index.razor",
        "PassPlat.Web/Pages/Federacion/IdenExt/Index.razor",
        "PassPlat.Web/Pages/Accesos/Index.razor",
        "PassPlat.Web/Pages/Bloqueos/Index.razor",
        "PassPlat.Web/Pages/IntentosAcceso/Index.razor",
        "PassPlat.Web/Pages/Roles/Index.razor",
        "PassPlat.Web/Pages/Grupos/Index.razor",
        "PassPlat.Web/Pages/Sesiones/Index.razor",
        "PassPlat.Web/Pages/MFA/Index.razor",
        "PassPlat.Web/Pages/Notificaciones/Index.razor",
        "PassPlat.Web/Pages/DispConfiables/Index.razor",
        "PassPlat.Web/Pages/Dispositivos/Index.razor",
        "PassPlat.Web/Pages/DominiosTenant/Index.razor",
        "PassPlat.Web/Pages/Email/Accounts.razor",
        "PassPlat.Web/Pages/PoliticasPwd/Index.razor",
        "PassPlat.Web/Pages/ConfigTenants/Index.razor",
        "PassPlat.Web/Pages/ConfigApp/Index.razor",
        "PassPlat.Web/Pages/EmailTemplates/Index.razor",
        "PassPlat.Web/Pages/Federacion/ProvIden/Index.razor",
    ];

    private const string UsuariosPagina = """
    {
      "items": [
        { "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "tenantNombre": "Plataforma", "apps": ["INVENTA_POS", "PASSPLAT"] },
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

    // S112 — contextos de admin_abarrotes (tenant 2): AccessPlat e Inventa POS; el de tenant 3 no debe ofrecerse.
    private const string ContextosAdmin = """
    [
      { "idTenant": 2, "tenantNombre": "Abarrotes del Sur", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [] },
      { "idTenant": 2, "tenantNombre": "Abarrotes del Sur", "idApp": 12, "appNombre": "Inventa POS", "appCodigo": "INVENTA", "roles": [] },
      { "idTenant": 3, "tenantNombre": "Vestuario SA de CV", "idApp": 13, "appNombre": "Inventa Store", "appCodigo": "INVENTA_STORE", "roles": [] }
    ]
    """;

    private readonly CapturaUrl _captura;

    public S85FiltrosListasTests()
    {
        // La base registra solo la ruta; aquí se guarda también la query para ver idTenant/idApp.
        _captura = new CapturaUrl(Handler);
        var http = new HttpClient(_captura) { BaseAddress = new Uri("https://localhost:5001/") };
        Services.AddSingleton(http);
        Services.AddSingleton(sp => new AuthService(http, sp.GetRequiredService<NavigationManager>(), sp.GetRequiredService<IJSRuntime>()));
        Services.AddSingleton(sp => new ApiClient(http, sp.GetRequiredService<AuthService>()));
        CargarCatalogos();
        Handler.Responder("/api/usuarios/page", UsuariosPagina);
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string LeerPagina(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), relativa.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Bloque de la barra: desde la barra (o el CrudToolbar) hasta la tabla.</summary>
    private static string BloqueBarra(string pagina)
    {
        var inicioBarra = pagina.IndexOf("table-toolbar filter-row", StringComparison.Ordinal);
        var inicioCrud = pagina.IndexOf("<CrudToolbar", StringComparison.Ordinal);
        var inicio = new[] { inicioBarra, inicioCrud }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        if (inicio < 0) return string.Empty;
        var fin = pagina.IndexOf("table-wrap", inicio, StringComparison.Ordinal);
        if (fin < 0) fin = pagina.IndexOf("<MudTable", inicio, StringComparison.Ordinal);
        return fin < 0 ? pagina[inicio..] : pagina[inicio..fin];
    }

    /// <summary>JWT como el real de sistema: TenantId del tenant Plataforma (1) y is_system. No es una clave real.</summary>
    private static string TokenSistemaConTenant(params string[] permisos)
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

    /// <summary>JWT sin TenantId y con is_system: sesión de sistema en modo plataforma (Auth.IdTenant = null, Auth.EsSistema).</summary>
    private static string TokenPlataforma(params string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("""{"alg":"HS256","typ":"JWT"}""");
        var payload = B64(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { ["is_system"] = "true", ["permiso"] = permisos }));
        return $"{header}.{payload}.firma-de-prueba";
    }

    private async Task<IRenderedComponent<UsuariosIndex>> RenderUsuariosPlataformaAsync()
    {
        await Auth.RestoreSessionAsync(TokenPlataforma("USUARIOS_VER", "APPS_VER"), null, 1, null, null, "sistema");
        return Render<UsuariosIndex>();
    }

    [Fact]
    public void Todas_las_listas_tienen_barra_table_toolbar_con_al_menos_un_filtro()
    {
        var fallos = new List<string>();
        foreach (var lista in Listas)
        {
            var pagina = LeerPagina(lista);
            var barra = BloqueBarra(pagina);
            if (barra.Length == 0)
            {
                fallos.Add($"{lista}: sin table-toolbar filter-row ni CrudToolbar");
                continue;
            }
            var filtros = Regex.Matches(barra, "lista-filtro|ShowSearch=\"true\"|<Filtros>").Count;
            if (filtros == 0) fallos.Add($"{lista}: barra sin filtros");
        }
        Assert.True(fallos.Count == 0, string.Join(Environment.NewLine, fallos));
    }

    [Fact]
    public void Ningun_filtro_de_barra_usa_FullWidth_ni_ancho_en_Style()
    {
        var fallos = new List<string>();
        foreach (var lista in Listas)
        {
            var barra = BloqueBarra(LeerPagina(lista));
            if (barra.Contains("FullWidth", StringComparison.Ordinal))
                fallos.Add($"{lista}: FullWidth en la barra");
            if (Regex.IsMatch(barra, "Style=\"[^\"]*width", RegexOptions.IgnoreCase))
                fallos.Add($"{lista}: ancho en Style en la barra");
        }
        Assert.True(fallos.Count == 0, string.Join(Environment.NewLine, fallos));
    }

    [Fact]
    public void CrudToolbar_dibuja_buscador_y_Filtros_en_la_misma_barra()
    {
        var cut = Render<CrudToolbar>(p => p
            .Add(c => c.ShowSearch, true)
            .Add(c => c.ShowCreate, false)
            .Add(c => c.Filtros, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "filtro-prueba");
                b.CloseElement();
            })));

        var barra = cut.Find("div.table-toolbar.filter-row");
        Assert.NotNull(barra.QuerySelector("input"));
        Assert.NotNull(barra.QuerySelector(".lista-filtro"));
        Assert.NotNull(barra.QuerySelector("span.filtro-prueba"));
        Assert.NotNull(barra.QuerySelector(".toolbar-spacer"));
        Assert.Empty(cut.FindAll(".mud-paper"));
    }

    [Fact]
    public async Task Usuarios_muestra_columnas_Tenant_y_Apps()
    {
        var cut = await RenderUsuariosPlataformaAsync();

        cut.WaitForAssertion(() => Assert.Contains("admin_abarrotes", cut.Markup));
        var cabeceras = cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToList();
        Assert.Contains("Tenant", cabeceras);
        Assert.Contains("Apps", cabeceras);
        Assert.Contains("Abarrotes del Sur", cut.Markup);
        Assert.Contains("INVENTA_POS", cut.Markup);
        Assert.Contains("PASSPLAT", cut.Markup);
    }

    [Fact]
    public async Task Usuarios_elegir_app_envia_idApp_a_la_api()
    {
        var cut = await RenderUsuariosPlataformaAsync();
        cut.WaitForAssertion(() => Assert.Contains("admin_abarrotes", cut.Markup));
        Assert.DoesNotContain(_captura.Urls, u => u.Contains("idApp=", StringComparison.Ordinal));

        var selector = cut.FindComponents<SelectorCatalogo<AppDto>>().Single();
        var app = selector.Instance.Items.First(a => a.Id == 3);
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(app));

        cut.WaitForAssertion(() => Assert.Contains(_captura.Urls,
            u => u.Contains("/api/usuarios/page", StringComparison.Ordinal) && u.Contains("&idApp=3", StringComparison.Ordinal)));
    }

    // S112 · D1 — el admin de tenant solo ve en «App» las apps de sus Accesos en su tenant, nunca el catálogo api/apps.
    [Fact]
    public async Task Usuarios_admin_de_tenant_lista_solo_las_apps_de_sus_accesos()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/5", ContextosAdmin);
        await AutenticarAsync(5, 2, "admin_abarrotes", "USUARIOS_VER", "APPS_VER", "PERMISOS_VER");
        var cut = Render<UsuariosIndex>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<SelectorCatalogo<AppDto>>()));
        var selector = cut.FindComponents<SelectorCatalogo<AppDto>>().Single();
        Assert.Equal(new[] { 1, 12 }, selector.Instance.Items.Select(a => a.Id).Order().ToArray());
        Assert.DoesNotContain(_captura.Urls, u => u.StartsWith("/api/apps", StringComparison.Ordinal));
    }

    // S112 · D2 — en Roles, el admin de tenant elige una app de sus Accesos y la lista pide los roles de esa app en su tenant.
    [Fact]
    public async Task Roles_admin_de_tenant_elegir_app_envia_idApp_a_la_api()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/5", ContextosAdmin);
        Handler.Responder("/api/roles/para-tenant/2", Datos.RolesJson());
        await AutenticarAsync(5, 2, "admin_abarrotes", "ROLES_DETALLE_VER", "PERMISOS_VER");
        var cut = Render<RolesIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<SelectorCatalogo<AppDto>>());
            Assert.Contains(_captura.Urls, u => u == "/api/roles/para-tenant/2");
        });
        var selector = cut.FindComponents<SelectorCatalogo<AppDto>>().Single();
        var app = selector.Instance.Items.Single(a => a.Id == 12);
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(app));

        cut.WaitForAssertion(() => Assert.Contains(_captura.Urls, u => u == "/api/roles/para-tenant/2?idApp=12"));
    }

    // S112 · D3 — el sistema lleva el tenant Plataforma en el token, pero Roles le pide todos los roles, no los de ese tenant.
    [Fact]
    public async Task Roles_sistema_pide_todos_los_roles_y_no_los_de_su_tenant()
    {
        await Auth.RestoreSessionAsync(TokenSistemaConTenant("ROLES_DETALLE_VER", "APPS_VER"), null, 1, 1, null, "sistema");
        var cut = Render<RolesIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(_captura.Urls, u => u == "/api/roles");
            Assert.Single(cut.FindComponents<SelectorCatalogo<AppDto>>());
        });
        Assert.DoesNotContain(_captura.Urls, u => u.StartsWith("/api/roles/para-tenant", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Usuarios_admin_de_tenant_no_ve_el_selector_de_tenant()
    {
        Handler.Responder("/api/UsuariosPermisos/contextos/5", ContextosAdmin);
        await AutenticarAsync(5, 2, "admin_abarrotes", "USUARIOS_VER", "APPS_VER", "PERMISOS_VER");
        var cut = Render<UsuariosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("admin_abarrotes", cut.Markup));
        Assert.Empty(cut.FindComponents<SelectorCatalogo<TenantDto>>());
        Assert.Single(cut.FindComponents<SelectorCatalogo<AppDto>>());
        Assert.DoesNotContain(_captura.Urls, u => u.Contains("idTenant=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tenants_filtro_estado_oculta_los_inactivos()
    {
        Handler.Responder("/api/tenants/page", """
        {
          "items": [
            { "id": 2, "codigo": "ABARROTES", "nombre": "Abarrotes del Sur", "activo": true, "esSistema": false },
            { "id": 9, "codigo": "CERRADO", "nombre": "Tenant Cerrado", "activo": false, "esSistema": false }
          ],
          "totalCount": 2, "pageNumber": 1, "pageSize": 100, "totalPages": 1, "hasPreviousPage": false, "hasNextPage": false
        }
        """);
        await AutenticarAsync(1, 1, "sistema", "TENANTS_VER");
        var cut = Render<TenantsIndex>();
        cut.WaitForAssertion(() => Assert.Contains("Tenant Cerrado", cut.Markup));

        var estado = cut.FindComponents<MudSelect<bool?>>().Single(s => s.Instance.Label == "Estado");
        await cut.InvokeAsync(() => estado.Instance.ValueChanged.InvokeAsync(true));

        cut.WaitForAssertion(() => Assert.DoesNotContain("Tenant Cerrado", cut.Markup));
        Assert.Contains("Abarrotes del Sur", cut.Markup);
    }

    [Fact]
    public async Task Accesos_filtro_app_deja_solo_esa_app()
    {
        Handler.Responder("/api/accesos/usuario/1", """
        [
          { "id": 11, "idUsuario": 1, "idTenant": 1, "idApp": 1, "idRol": 1, "activo": true, "appNombre": "Inventa POS", "rolNombre": "Administrador", "usuarioNombre": "sistema", "tenantNombre": "Plataforma" },
          { "id": 12, "idUsuario": 1, "idTenant": 1, "idApp": 2, "idRol": 2, "activo": true, "appNombre": "Inventa Store", "rolNombre": "Cajero", "usuarioNombre": "sistema", "tenantNombre": "Plataforma" }
        ]
        """);
        await AutenticarAsync();
        var cut = Render<AccesosIndex>();
        cut.WaitForAssertion(() => Assert.Contains("Inventa Store", cut.Markup));

        var app = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "App");
        await cut.InvokeAsync(() => app.Instance.ValueChanged.InvokeAsync("Inventa POS"));

        cut.WaitForAssertion(() =>
        {
            var filas = cut.FindAll("tbody tr.mud-table-row");
            Assert.Single(filas);
            Assert.Contains("Inventa POS", filas[0].TextContent);
        });
    }

    /// <summary>Envuelve el handler de la base y guarda la ruta con su query.</summary>
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
