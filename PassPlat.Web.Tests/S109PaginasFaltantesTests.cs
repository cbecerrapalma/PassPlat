using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Tests.Infra;
using AppsModulosIndex = PassPlat.Web.Pages.AppsModulos.Index;
using AsignarModuloDialog = PassPlat.Web.Pages.AppsModulos.AsignarModuloDialog;
using ConfirmDialog = PassPlat.Web.Shared.ConfirmDialog;
using HerenciaIndex = PassPlat.Web.Pages.RolesHerencia.Index;
using HistorialPage = PassPlat.Web.Pages.Email.Historial;
using ModuloDialog = PassPlat.Web.Pages.Modulos.ModuloDialog;
using ModulosIndex = PassPlat.Web.Pages.Modulos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S109 · D3–D7 — las cuatro páginas del catálogo que faltaban: guarda por el permiso del módulo,
/// listas en el patrón único, escritura solo para el sistema y errores de la API sin romper.
/// </summary>
public class S109PaginasFaltantesTests : WebTestBase
{
    private const string HerenciaJson = """
    [ { "id": 5, "idRolHijo": 12, "idRolPadre": 10, "idTenant": 2, "activo": true, "fecCrea": "2026-01-05T10:00:00",
        "rolHijoNombre": "Cajero", "rolPadreNombre": "Supervisor", "tenantNombre": "Abarrotes del Sur" } ]
    """;

    private const string ModulosJson = """
    [ { "id": 1, "idTipoModulo": 1, "codigo": "ADMIN", "nombre": "Administracion", "ruta": null, "orden": 1,
        "esVisibleMenu": true, "activo": true, "fecCrea": "2026-01-01T00:00:00",
        "subModulos": [
          { "id": 2, "idModuloPadre": 1, "idTipoModulo": 1, "codigo": "ROLES", "nombre": "Roles", "ruta": "/admin/roles",
            "orden": 1, "esVisibleMenu": true, "activo": true, "fecCrea": "2026-01-01T00:00:00", "subModulos": [] },
          { "id": 3, "idModuloPadre": 1, "idTipoModulo": 1, "codigo": "USUARIOS", "nombre": "Usuarios", "ruta": "/admin/usuarios",
            "orden": 2, "esVisibleMenu": true, "activo": false, "fecCrea": "2026-01-01T00:00:00", "subModulos": [] }
        ] } ]
    """;

    private const string TiposModuloJson = """
    [ { "id": 1, "codigo": "ADMIN", "nombre": "Administracion", "descripcion": null, "activo": true } ]
    """;

    private const string AppsJson = """
    [ { "id": 3, "codigo": "INVENTA_POS", "nombre": "Inventa POS", "descripcion": null, "activo": true } ]
    """;

    private const string AppModulosJson = """
    [ { "id": 21, "idApp": 3, "idModulo": 2, "activo": true, "fecCrea": "2026-01-06T08:00:00",
        "appNombre": "Inventa POS", "moduloNombre": "Roles", "moduloCodigo": "ROLES" } ]
    """;

    private const string HistorialJson = """
    [ { "id": 9, "idTenant": 2, "idUsuario": 1, "destinatario": "ana@a.test", "asunto": "Bienvenida",
        "estado": "fallido", "proveedor": "SMTP", "intentos": 3, "errorDetalle": "SMTP 550 buzon no existe",
        "extraJson": "CUERPO-SECRETO", "fecCrea": "2026-11-01T09:30:00" } ]
    """;

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    /// <summary>JWT de prueba con el claim <c>is_system</c>. No es una clave real.</summary>
    private static string TokenConSistema(int idTenant, bool esSistema, string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = new Dictionary<string, object> { ["TenantId"] = idTenant.ToString(), ["permiso"] = permisos };
        if (esSistema) payload["is_system"] = "true";
        return $"{B64("""{"alg":"HS256","typ":"JWT"}""")}.{B64(System.Text.Json.JsonSerializer.Serialize(payload))}.firma-de-prueba";
    }

    private Task IniciarSesionAsync(bool esSistema, int idTenant, params string[] permisos) =>
        Auth.RestoreSessionAsync(TokenConSistema(idTenant, esSistema, permisos), null, 1, idTenant, null,
            esSistema ? "sistema" : "admin_tenant");

    [Fact]
    public async Task Herencia_sin_permiso_muestra_SinPermiso()
    {
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "ROLES_VER");

        var cut = Render<HerenciaIndex>();

        cut.WaitForAssertion(() => Assert.Contains("ROLES_HERENCIA_VER", cut.Markup));
        Assert.Empty(cut.FindAll("table"));
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("api/RolesHerencia", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Herencia_lista_las_filas_del_tenant_y_quitar_llama_DELETE()
    {
        var referencia = new Mock<IDialogReference>();
        referencia.Setup(r => r.Result).Returns(Task.FromResult<DialogResult?>(DialogResult.Ok(true)));
        var dialogos = new Mock<IDialogService>();
        dialogos.Setup(d => d.ShowAsync<ConfirmDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>()))
            .ReturnsAsync(referencia.Object);
        Services.AddSingleton(dialogos.Object);

        Handler.Responder("/api/tenants", Datos.TenantsJson());
        Handler.Responder("/api/RolesHerencia/tenant/1", HerenciaJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "ROLES_HERENCIA_VER", "ROLES_EDITAR");

        var cut = Render<HerenciaIndex>();

        cut.WaitForAssertion(() => Assert.Equal("Supervisor", cut.Find("[data-testid=herencia-fila]").TextContent.Trim()));
        Assert.Contains("Cajero", cut.Markup);
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/RolesHerencia/tenant/1", StringComparison.Ordinal));

        cut.Find("[data-testid=herencia-quitar]").Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.StartsWith("DELETE /api/RolesHerencia/5", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Modulos_aplana_el_arbol_y_filtra_por_estado()
    {
        Handler.Responder("/api/TiposModulo", TiposModuloJson);
        Handler.Responder("/api/modulos", ModulosJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "MODULOS_VER", "PERMISOS_VER", "PERMISOS_CREAR", "PERMISOS_EDITAR");

        var cut = Render<ModulosIndex>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=modulo-fila]").Count));

        var estado = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Estado");
        await cut.InvokeAsync(() => estado.Instance.ValueChanged.InvokeAsync("activos"));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid=modulo-fila]").Count));
        Assert.DoesNotContain("USUARIOS", cut.Markup);
    }

    [Fact]
    public async Task Modulos_sin_ser_sistema_no_muestra_botones_de_escritura()
    {
        Handler.Responder("/api/TiposModulo", TiposModuloJson);
        Handler.Responder("/api/modulos", ModulosJson);
        await IniciarSesionAsync(esSistema: false, idTenant: 2, "MODULOS_VER", "PERMISOS_VER", "PERMISOS_CREAR", "PERMISOS_EDITAR");

        var cut = Render<ModulosIndex>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=modulo-fila]").Count));
        Assert.Empty(cut.FindAll("[data-testid=modulos-nuevo]"));
        Assert.Empty(cut.FindAll("[data-testid=modulos-editar]"));
    }

    [Fact]
    public async Task Modulos_al_editar_el_selector_de_padre_excluye_al_modulo_y_a_sus_descendientes()
    {
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "MODULOS_VER", "PERMISOS_EDITAR");
        var cut = Render<TestHost>();
        static ModuloDto Mod(int id, int? padre, string codigo) =>
            new() { Id = id, IdModuloPadre = padre, IdTipoModulo = 1, Codigo = codigo, Nombre = codigo, Activo = true };
        var admin = Mod(1, null, "ADMIN");
        var planos = new List<ModuloDto> { admin, Mod(2, 1, "ROLES"), Mod(3, 1, "USUARIOS"), Mod(4, 3, "PERFIL"), Mod(5, null, "EMAIL") };

        await Services.GetRequiredService<IDialogService>().ShowAsync<ModuloDialog>("Editar módulo",
            new DialogParameters { ["Modulo"] = admin, ["Modulos"] = planos, ["Tipos"] = new List<TipoModuloDto>() });

        cut.WaitForAssertion(() => cut.Find("[data-testid=modulo-padre]"));
        var ofrecidos = cut.FindComponents<MudSelectItem<int?>>().Select(i => i.Instance.Value).ToList();
        Assert.Contains(5, ofrecidos);
        Assert.DoesNotContain(1, ofrecidos);
        Assert.DoesNotContain(2, ofrecidos);
        Assert.DoesNotContain(3, ofrecidos);
        Assert.DoesNotContain(4, ofrecidos);
    }

    [Fact]
    public async Task AppsModulos_sin_app_muestra_elige_una_app()
    {
        Handler.Responder("/api/apps", AppsJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "APPSMODULOS_VER", "APPS_EDITAR");

        var cut = Render<AppsModulosIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Elige una app", cut.Find("[data-testid=appsmodulos-vacio]").TextContent));
        Assert.Empty(cut.FindAll("[data-testid=appmodulo-fila]"));
    }

    [Fact]
    public async Task AppsModulos_asignar_hace_POST_con_IdApp_e_IdModulo()
    {
        Handler.Responder("/api/apps", AppsJson);
        Handler.Responder("/api/modulos", ModulosJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "APPSMODULOS_VER", "APPS_EDITAR");
        var cut = Render<TestHost>();

        await Services.GetRequiredService<IDialogService>().ShowAsync<AsignarModuloDialog>("Asignar módulo",
            new DialogParameters { ["IdApp"] = 3, ["IdsAsignados"] = new List<int>() });

        cut.WaitForAssertion(() => cut.Find("[data-testid=appmodulo-select]"));
        var selector = cut.FindComponents<MudSelect<int>>().Single();
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(3));
        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p =>
            p.StartsWith("POST /api/AppsModulos", StringComparison.OrdinalIgnoreCase)
            && p.Contains("\"idApp\":3", StringComparison.Ordinal)
            && p.Contains("\"idModulo\":3", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AppsModulos_si_el_servidor_rechaza_la_asignacion_el_error_queda_en_el_dialogo()
    {
        Handler.Responder("/api/apps", AppsJson);
        Handler.Responder("/api/modulos", ModulosJson);
        Handler.ResponderConError("/api/AppsModulos", HttpStatusCode.BadRequest, """{ "detail": "El módulo ya está asignado a la app" }""");
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "APPSMODULOS_VER", "APPS_EDITAR");
        var cut = Render<TestHost>();

        await Services.GetRequiredService<IDialogService>().ShowAsync<AsignarModuloDialog>("Asignar módulo",
            new DialogParameters { ["IdApp"] = 3, ["IdsAsignados"] = new List<int>() });

        cut.WaitForAssertion(() => cut.Find("[data-testid=appmodulo-select]"));
        var selector = cut.FindComponents<MudSelect<int>>().Single();
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(3));
        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        // S115 · D2 — el error del servidor queda dentro del diálogo, que sigue abierto para reintentar
        cut.WaitForAssertion(() => Assert.Contains("El módulo ya está asignado a la app", cut.Find("[data-testid=alerta-error-dialogo]").TextContent));
        Assert.NotEmpty(cut.FindAll("[data-testid=appmodulo-select]"));
        Assert.DoesNotContain(Services.GetRequiredService<ISnackbar>().ShownSnackbars, s => s.Severity == Severity.Error);
    }

    [Fact]
    public async Task Historial_muestra_filas_y_chip_de_error_sin_cuerpo_del_correo()
    {
        Handler.Responder("/api/EmailLog", HistorialJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "EMAIL_LOG_VER");

        var cut = Render<HistorialPage>();

        cut.WaitForAssertion(() => Assert.Equal("ana@a.test", cut.Find("[data-testid=envio-destinatario]").TextContent.Trim()));
        Assert.Contains("fallido", cut.Markup);
        Assert.Contains("Bienvenida", cut.Markup);
        Assert.DoesNotContain("CUERPO-SECRETO", cut.Markup);
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/EmailLog", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Historial_ante_403_muestra_el_error_sin_romper()
    {
        Handler.ResponderConError("/api/EmailLog", HttpStatusCode.Forbidden, """{ "title": "Prohibido" }""");
        await IniciarSesionAsync(esSistema: true, idTenant: 1, "EMAIL_LOG_VER");

        var cut = Render<HistorialPage>();

        cut.WaitForAssertion(() => Assert.Contains("No hay envíos registrados", cut.Markup));
        var avisos = Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message ?? string.Empty).ToList();
        Assert.Contains(avisos, a => a.Contains("Prohibido", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid=envio-fila]"));
    }

    [Fact]
    public void Las_cuatro_paginas_cumplen_el_patron_de_listas_H10()
    {
        var paginas = new (string Ruta, string Url)[]
        {
            ("PassPlat.Web/Pages/RolesHerencia/Index.razor", "/admin/herencia-roles"),
            ("PassPlat.Web/Pages/Modulos/Index.razor", "/admin/modulos"),
            ("PassPlat.Web/Pages/AppsModulos/Index.razor", "/admin/apps-modulos"),
            ("PassPlat.Web/Pages/Email/Historial.razor", "/email/historial")
        };

        var faltantes = new List<string>();
        foreach (var (ruta, url) in paginas)
        {
            var texto = File.ReadAllText(Path.Combine(RaizDelRepo(), ruta.Replace('/', Path.DirectorySeparatorChar)));
            if (!texto.Contains($"@page \"{url}\"", StringComparison.Ordinal)) faltantes.Add($"{ruta} -> @page {url}");
            foreach (var marca in new[] { "class=\"iam-page\"", "class=\"page-header\"", "class=\"page-title\"", "class=\"table-panel\"", "class=\"table-wrap\"" })
            {
                if (!texto.Contains(marca, StringComparison.Ordinal)) faltantes.Add($"{ruta} -> {marca}");
            }
            if (texto.Contains("<MudContainer", StringComparison.Ordinal)) faltantes.Add($"{ruta} -> <MudContainer");
            if (texto.Contains("Typo=\"Typo.h5\"", StringComparison.Ordinal)) faltantes.Add($"{ruta} -> Typo.h5");
            if (texto.Contains("CrudToolbar Title=", StringComparison.Ordinal)) faltantes.Add($"{ruta} -> CrudToolbar Title");
            if (texto.Contains("\" / aria-label", StringComparison.Ordinal)) faltantes.Add($"{ruta} -> barra suelta antes de aria-label");
        }

        Assert.Empty(faltantes);
    }
}
