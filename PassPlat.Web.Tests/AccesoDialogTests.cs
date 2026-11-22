using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Pages.Accesos;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S54 · K2 — dialogo de asignacion de acceso (D1), abierto con el IDialogService real.
/// S54.1 · K2 (D5/D6/D7) — aviso de reemplazo exacto, español correcto y validacion visible.
/// </summary>
public class AccesoDialogTests : WebTestBase
{
    public AccesoDialogTests() => CargarCatalogos();

    private IRenderedComponent<TestHost> RenderHost()
        => Render<TestHost>(p => p.AddChildContent<MudDialogProvider>());

    private async Task<IRenderedComponent<TestHost>> AbrirDialogoAsync(int? usuarioId = null, int? appId = null)
    {
        // S113 · D4 — el diálogo pide api/tenants y api/apps solo con TENANTS_VER/APPS_VER (sesión de plataforma)
        await AutenticarAsync(permisos: ["ACCESOS_VER", "ACCESOS_ASIGNAR", "ACCESOS_REVOCAR", "USUARIOS_VER", "TENANTS_VER", "APPS_VER"]);
        var cut = RenderHost();
        var servicio = Services.GetRequiredService<IDialogService>();
        var parametros = new DialogParameters<AccesoDialog>
        {
            { x => x.UsuarioIdPreseleccionado, usuarioId },
            { x => x.AppIdPreseleccionado, appId }
        };
        await servicio.ShowAsync<AccesoDialog>("Asignar Acceso", parametros);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-asignar]")));
        return cut;
    }

    private static IElement InputDe<T>(IRenderedComponent<T> cut, string etiqueta) where T : IComponent
    {
        var label = cut.FindAll("label")
            .First(l => l.TextContent.Trim().StartsWith(etiqueta, StringComparison.OrdinalIgnoreCase));
        var id = label.GetAttribute("for");
        return cut.Find($"input#{id}");
    }

    private static void Elegir<T>(IRenderedComponent<T> cut, string etiqueta, string texto) where T : IComponent
    {
        var input = InputDe(cut, etiqueta);
        input.Focus();
        input.Input(texto);
    }

    private static void ElegirOpcion<T>(IRenderedComponent<T> cut, string texto) where T : IComponent
        => cut.FindAll(".mud-list-item").First(e => e.TextContent.Contains(texto)).Click();

    [Fact]
    public async Task El_boton_asignar_esta_deshabilitado_mientras_faltan_campos()
    {
        var cut = await AbrirDialogoAsync();

        Assert.True(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task El_tenant_no_es_editable()
    {
        var cut = await AbrirDialogoAsync();

        var tenant = InputDe(cut, "Tenant");
        Assert.True(tenant.HasAttribute("readonly"));
        Assert.True(tenant.HasAttribute("disabled"));
        Assert.Contains("El acceso se crea en el tenant de tu sesión", cut.Markup);
    }

    [Fact]
    public async Task Los_roles_se_filtran_a_activos_globales_o_del_tenant_de_la_sesion()
    {
        var cut = await AbrirDialogoAsync();

        InputDe(cut, "Rol").Focus();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        var textos = cut.FindAll(".mud-list-item").Select(e => e.TextContent).ToList();
        Assert.Equal(4, textos.Count);
        Assert.Contains(textos, t => t.Contains("PLATFORM_ADMIN"));
        Assert.DoesNotContain(textos, t => t.Contains("ABARROTES_ADMIN"));
        Assert.DoesNotContain(textos, t => t.Contains("E2E_ADMIN"));
    }

    [Fact]
    public async Task El_aviso_de_reemplazo_describe_el_rol_y_el_tenant_del_acceso_existente()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Consultor")));
        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "Editor");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Editor (PLATFORM_EDITOR)");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-reemplazo]")));
        var aviso = cut.Find("[data-testid=acceso-reemplazo]").TextContent;
        Assert.Contains("Reemplaza el rol Consultor (tenant Plataforma) por Editor en el tenant de tu sesión (Plataforma)", aviso);
    }

    [Fact]
    public async Task Con_el_mismo_rol_y_tenant_no_hay_cambios_que_guardar_y_asignar_esta_deshabilitado()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Administrador")));
        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "Administrador");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Administrador (PLATFORM_ADMIN)");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-reemplazo]")));
        var aviso = cut.Find("[data-testid=acceso-reemplazo]").TextContent;
        Assert.Contains("Ya tiene el rol Administrador en el tenant Plataforma para esta app: no hay cambios que guardar.", aviso);
        Assert.True(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Salir_de_un_campo_obligatorio_vacio_muestra_su_mensaje()
    {
        var cut = await AbrirDialogoAsync();

        var usuario = InputDe(cut, "Usuario");
        usuario.Focus();
        cut.WaitForState(() => true, TimeSpan.FromMilliseconds(200));
        usuario.Blur();

        cut.WaitForAssertion(() => Assert.Contains("El usuario es requerido", cut.Markup));
    }

    [Fact]
    public async Task La_confirmacion_muestra_el_mismo_texto_de_reemplazo()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Consultor")));
        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "Editor");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Editor (PLATFORM_EDITOR)");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-reemplazo]")));
        cut.Find("[data-testid=acceso-asignar]").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirmar reemplazo", cut.Markup));
        Assert.Contains(
            "Reemplaza el rol Consultor (tenant Plataforma) por Editor en el tenant de tu sesión (Plataforma)",
            cut.Markup);
        // El POST no debe salir: la confirmacion sigue abierta.
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("POST /api/accesos/asignar"));
    }

    [Fact]
    public async Task El_error_del_servidor_se_muestra()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson());
        Handler.StatusAsignar = System.Net.HttpStatusCode.BadRequest;
        Handler.BodyAsignar = """{"detail":"El usuario no pertenece al tenant","codigo":"ACCESO_DENEGADO"}""";

        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "PLATFORM_ADMIN");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "PLATFORM_ADMIN");

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled")));

        cut.Find("[data-testid=acceso-asignar]").Click();

        // S115 · D2 — el error del servidor queda dentro del diálogo (AlertaErrorDialogo), no en el Snackbar
        cut.WaitForAssertion(() => Assert.Contains("El usuario no pertenece al tenant", cut.Find("[data-testid=alerta-error-dialogo]").TextContent));
        Assert.DoesNotContain(MensajesSnackbar(), m => m.Contains("El usuario no pertenece al tenant"));
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("POST /api/accesos/asignar"));
    }

    [Fact]
    public async Task Con_usuario_app_y_rol_el_boton_asignar_se_habilita()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson());
        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Assert.True(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled"));

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "PLATFORM_ADMIN");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "PLATFORM_ADMIN");

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled")));
    }

    /// <summary>
    /// S54.2 · K2 (D2) — con usuario y app precargados y sin rol, el aviso describe el acceso
    /// que ya existe pero no dice "no hay cambios"; el boton sigue deshabilitado.
    /// </summary>
    [Fact]
    public async Task En_el_dialogo_sin_rol_el_aviso_no_dice_que_no_hay_cambios()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 12, 1, "Administrador")));
        var cut = await AbrirDialogoAsync(usuarioId: 1, appId: 12);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=acceso-reemplazo]")));
        var aviso = cut.Find("[data-testid=acceso-reemplazo]").TextContent;

        Assert.Contains("Ya tiene el rol Administrador en el tenant Plataforma para esta app.", aviso);
        Assert.DoesNotContain("no hay cambios", aviso);
        Assert.True(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled"));
    }

    /// <summary>S61 - K4: el rechazo SIN_MEMBRESIA_TENANT del servidor llega al dialogo.</summary>
    [Fact]
    public async Task El_rechazo_SIN_MEMBRESIA_TENANT_se_muestra_en_el_dialogo()
    {
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson());
        Handler.StatusAsignar = System.Net.HttpStatusCode.BadRequest;
        Handler.BodyAsignar = """{"detail":"El usuario no pertenece a este tenant","errorCode":"SIN_MEMBRESIA_TENANT"}""";

        var cut = await AbrirDialogoAsync(usuarioId: 1);

        Elegir(cut, "App", "INVENTA");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "Inventa POS (INVENTA)");
        Elegir(cut, "Rol", "PLATFORM_ADMIN");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ElegirOpcion(cut, "PLATFORM_ADMIN");

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=acceso-asignar]").HasAttribute("disabled")));
        cut.Find("[data-testid=acceso-asignar]").Click();

        // S115 · D2 — el rechazo queda dentro del diálogo (AlertaErrorDialogo), no en el Snackbar
        cut.WaitForAssertion(() => Assert.Contains("El usuario no pertenece a este tenant", cut.Find("[data-testid=alerta-error-dialogo]").TextContent));
        Assert.DoesNotContain(MensajesSnackbar(), m => m.Contains("El usuario no pertenece a este tenant"));
    }

    private List<string> MensajesSnackbar() =>
        [.. Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message ?? "")];
}
