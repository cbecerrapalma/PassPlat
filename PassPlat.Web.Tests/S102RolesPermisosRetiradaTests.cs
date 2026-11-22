using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Services;
using PassPlat.Web.Tests.Infra;
using RolDetail = PassPlat.Web.Pages.Roles.RolDetail;
using UsuarioDetail = PassPlat.Web.Pages.Usuarios.UsuarioDetail;

namespace PassPlat.Web.Tests;

/// <summary>
/// S102 — retiro del maestro-detalle de Roles y Permisos: la página queda corta (permisos
/// directos), la Política pasa a RolDetail, los Permisos directos a UsuarioDetail y el panel
/// lleva la configuración de seguridad a Roles. S115 revierte D7: el error del servidor va dentro del diálogo.
/// </summary>
public class S102RolesPermisosRetiradaTests : WebTestBase
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(params string[] partes) =>
        File.ReadAllText(Path.Combine([RaizDelRepo(), "PassPlat.Web", .. partes]));

    private static IEnumerable<string> Pestanas<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(".mud-tab").Select(t => t.TextContent.Trim());

    [Fact]
    public void La_pagina_de_permisos_directos_es_corta_y_sin_pestanas()
    {
        var texto = Leer("Pages", "RolesPermisos", "Index.razor");
        var lineas = texto.Split('\n').Length;

        Assert.True(lineas < 150, $"la página tiene {lineas} líneas");
        Assert.Contains("@page \"/admin/roles-permisos\"", texto);
        Assert.DoesNotContain("<MudTabs", texto);
        Assert.DoesNotContain("new BreadcrumbItem(", texto);
        Assert.Contains("<MigasDePan Actual=\"Permisos directos\"", texto);
        Assert.Contains("<SinPermiso PermisoRequerido=\"PERMISOS_VER\"", texto);
        Assert.Contains("<UsuarioPermisos ", texto);
    }

    [Fact]
    public async Task RolDetail_muestra_la_pestana_Politica_con_POLITICASPWD_VER()
    {
        Handler.Responder("/api/roles/para-tenant/1", """[{ "id": 1, "codigo": "ADMIN", "nombre": "Administrador", "idTenant": null, "tenantNombre": null, "activo": true }]""");
        Handler.Responder("/api/roles", """[{ "id": 1, "codigo": "ADMIN", "nombre": "Administrador", "idTenant": null, "tenantNombre": null, "activo": true }]""");

        await AutenticarAsync(1, 1, "sistema", "ROLES_DETALLE_VER", "POLITICASPWD_VER");
        var con = Render<RolDetail>(p => p.Add(x => x.Id, 1));
        con.WaitForAssertion(() => Assert.Contains("Herencia", Pestanas(con)), TimeSpan.FromSeconds(15));
        Assert.Contains("Política", Pestanas(con));

        var orden = Pestanas(con).ToList();
        Assert.Equal(orden.IndexOf("Herencia") + 1, orden.IndexOf("Política"));

        await AutenticarAsync(1, 1, "sistema", "ROLES_DETALLE_VER");
        var sin = Render<RolDetail>(p => p.Add(x => x.Id, 1));
        sin.WaitForAssertion(() => Assert.Contains("Herencia", Pestanas(sin)), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain("Política", Pestanas(sin));
    }

    private const string RolAdminJson = """[{ "id": 1, "codigo": "ADMIN", "nombre": "Administrador", "idTenant": null, "tenantNombre": null, "activo": true }]""";

    /// <summary>
    /// Busca la pestaña y hace clic dentro del despachador del renderer: así el clic no compite
    /// con los re-renders de las cargas iniciales y nunca usa el id de un manejador ya retirado.
    /// </summary>
    private static Task ClicEnPestanaAsync<T>(IRenderedComponent<T> cut, string texto) where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.InvokeAsync(() => cut.FindAll(".mud-tab").First(t => t.TextContent.Trim() == texto).Click());

    /// <summary>
    /// Registra la petición al instante y entrega la respuesta después, como la red real.
    /// Con la respuesta síncrona del doble, la primera carga termina antes del segundo render
    /// de RolDetail y la doble carga no se ve.
    /// </summary>
    private sealed class RespuestaDiferida(HttpMessageHandler interno) : DelegatingHandler(interno)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var respuesta = await base.SendAsync(request, cancellationToken);
            await Task.Delay(20, cancellationToken);
            return respuesta;
        }
    }

    [Fact]
    public async Task RolDetail_carga_la_Politica_una_sola_vez_al_activar_la_pestana()
    {
        var http = new HttpClient(new RespuestaDiferida(Handler)) { BaseAddress = new Uri("https://localhost:5001/") };
        Services.AddSingleton(sp => new ApiClient(http, sp.GetRequiredService<AuthService>()));

        Handler.Responder("/api/roles/para-tenant/1", RolAdminJson);
        Handler.Responder("/api/rolespoliticaspwd/rol/1", """[{ "id": 7, "idTenant": 1, "idRol": 1, "idPolitica": 2, "activo": true, "politicaNombre": "Politica Estandar" }]""");
        Handler.Responder("/api/politicaspwd", """[{ "id": 2, "codigo": "STD", "nombre": "Politica Estandar", "longMin": 8, "longMax": 64, "activa": true }]""");

        await AutenticarAsync(1, 1, "sistema", "ROLES_DETALLE_VER", "POLITICASPWD_VER");
        var cut = Render<RolDetail>(p => p.Add(x => x.Id, 1));
        cut.WaitForAssertion(() => Assert.Contains("Política", Pestanas(cut)), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("politicaspwd", StringComparison.OrdinalIgnoreCase));

        await ClicEnPestanaAsync(cut, "Política");

        cut.WaitForAssertion(() => Assert.Contains("Requisitos de Complejidad", cut.Markup), TimeSpan.FromSeconds(15));
        Assert.Single(Handler.Peticiones, p => p == "GET /api/rolespoliticaspwd/rol/1");
        Assert.Single(Handler.Peticiones, p => p == "GET /api/politicaspwd");
    }

    [Fact]
    public async Task RolDetail_muestra_Gestionar_Herencia_solo_con_ROLES_EDITAR()
    {
        Handler.Responder("/api/roles/para-tenant/1", RolAdminJson);

        await AutenticarAsync(1, 1, "sistema", "ROLES_DETALLE_VER");
        var sin = Render<RolDetail>(p => p.Add(x => x.Id, 1));
        sin.WaitForAssertion(() => Assert.Contains("Herencia", Pestanas(sin)), TimeSpan.FromSeconds(15));
        await ClicEnPestanaAsync(sin, "Herencia");
        sin.WaitForAssertion(() => Assert.Contains("Sin roles padre", sin.Markup), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain("Gestionar Herencia", sin.Markup);

        await AutenticarAsync(1, 1, "sistema", "ROLES_DETALLE_VER", "ROLES_EDITAR");
        var con = Render<RolDetail>(p => p.Add(x => x.Id, 1));
        con.WaitForAssertion(() => Assert.Contains("Herencia", Pestanas(con)), TimeSpan.FromSeconds(15));
        await ClicEnPestanaAsync(con, "Herencia");
        con.WaitForAssertion(() => Assert.Contains("Sin roles padre", con.Markup), TimeSpan.FromSeconds(15));
        Assert.Contains("Gestionar Herencia", con.Markup);
    }

    [Fact]
    public async Task UsuarioDetail_muestra_la_pestana_Permisos_con_PERMISOS_VER()
    {
        Handler.Responder("/api/usuarios/1", """{ "id": 1, "idTenant": 1, "nomUsuario": "sistema", "nombre": "Usuario", "apellido": "Sistema", "eliminado": false }""");

        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER", "PERMISOS_VER");
        var con = Render<UsuarioDetail>(p => p.Add(x => x.Id, 1));
        con.WaitForAssertion(() => Assert.Contains(Pestanas(con), t => t.StartsWith("Identidades", StringComparison.Ordinal)), TimeSpan.FromSeconds(15));
        Assert.Equal("Permisos", Pestanas(con).Last());

        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER");
        var sin = Render<UsuarioDetail>(p => p.Add(x => x.Id, 1));
        sin.WaitForAssertion(() => Assert.Contains(Pestanas(sin), t => t.StartsWith("Identidades", StringComparison.Ordinal)), TimeSpan.FromSeconds(15));
        Assert.DoesNotContain(Pestanas(sin), t => t.StartsWith("Permisos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_dialogo_muestra_el_error_del_servidor_dentro_y_sigue_abierto()
    {
        // S115 · D2 — revierte S102 · D7: el error del servidor queda en el diálogo, no en el Snackbar
        Handler.ResponderConError("/pepper-version", System.Net.HttpStatusCode.BadRequest, """{ "detail": "La versión de pepper no es válida" }""");
        await AutenticarAsync(1, 1, "sistema", "CONFIGTENANTS_VER");
        var cut = Render<TestHost>();

        await Services.GetRequiredService<MudBlazor.IDialogService>().ShowAsync<PassPlat.Web.Pages.ConfigTenants.PepperVersionDialog>("Pepper",
            new MudBlazor.DialogParameters { ["ConfigTenant"] = new PassPlat.Aplicacion.Dtos.Catalogos.ConfigTenantDto { Id = 7, IdTenant = 1, PepperVersionActual = 1, TenantNombre = "Demo" } });

        cut.WaitForAssertion(() => cut.Find("[data-testid=boton-guardar-dialogo]"));
        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        cut.WaitForAssertion(() => Assert.Contains("La versión de pepper no es válida", cut.Find("[data-testid=alerta-error-dialogo]").TextContent));
        Assert.NotEmpty(cut.FindAll("[data-testid=boton-guardar-dialogo]"));
        Assert.DoesNotContain(Services.GetRequiredService<MudBlazor.ISnackbar>().ShownSnackbars, s => s.Severity == MudBlazor.Severity.Error);
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("POST /api/ConfigTenants/7/pepper-version", StringComparison.Ordinal));
    }

    [Fact]
    public void El_panel_lleva_la_configuracion_de_seguridad_a_Roles()
    {
        var panel = Leer("Pages", "Dashboard.razor");

        Assert.DoesNotContain("Href=\"/admin/roles-permisos\"", panel);
        Assert.Contains("<MudButton Href=\"/admin/roles\"", panel);
        Assert.DoesNotContain(".rp-", Leer("wwwroot", "css", "iam-pages.css"));
    }
}
