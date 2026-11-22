using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Pages.DominiosTenant;
using PassPlat.Web.Pages.Email;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S101 · D4 — las páginas de Email dejan de simular: listan la API, el catálogo global solo lo
/// escribe el sistema y un admin de tenant ve únicamente las cuentas de su tenant.
/// </summary>
public class S101EmailPaginasTests : WebTestBase
{
    private const string ProvidersJson = """
    [
      { "id": 1, "codigo": "SMTP", "nombre": "SMTP", "descripcion": "Protocolo estándar", "activo": true },
      { "id": 2, "codigo": "SENDGRID", "nombre": "SendGrid", "descripcion": null, "activo": true }
    ]
    """;

    private const string CuentasJson = """
    [ { "id": 1, "idProvider": 1, "nombre": "SMTP Global", "host": "smtp.test", "puerto": 587, "smtpUsuario": "u", "fromAddress": "no-reply@s101.test", "activo": true, "esPredeterminada": true, "providerNombre": "SMTP" } ]
    """;

    private static readonly string[] PermisosEmail =
        ["EMAIL_PROVIDERS_VER", "EMAIL_ACCOUNTS_VER", "EMAIL_APP_ACCTS_VER", "EMAIL_TENANT_ACCTS_VER"];

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    /// <summary>JWT de prueba con el claim <c>is_system</c> (AutenticarAsync de la base no lo emite). No es una clave real.</summary>
    private static string TokenConSistema(int idTenant, bool esSistema, string[] permisos)
    {
        static string B64(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = new Dictionary<string, object> { ["TenantId"] = idTenant.ToString(), ["permiso"] = permisos };
        if (esSistema) payload["is_system"] = "true";
        return $"{B64("""{"alg":"HS256","typ":"JWT"}""")}.{B64(System.Text.Json.JsonSerializer.Serialize(payload))}.firma-de-prueba";
    }

    private Task IniciarSesionAsync(bool esSistema, int idTenant) =>
        Auth.RestoreSessionAsync(TokenConSistema(idTenant, esSistema, PermisosEmail), null, 1, idTenant, null, esSistema ? "sistema" : "admin_tenant");

    [Fact]
    public void Ninguna_pagina_de_email_tiene_datos_simulados()
    {
        var carpeta = Path.Combine(RaizDelRepo(), "PassPlat.Web", "Pages", "Email");
        var pendientes = new List<string>();
        foreach (var archivo in Directory.GetFiles(carpeta, "*.razor"))
        {
            var fuente = File.ReadAllText(archivo);
            foreach (var prohibido in new[] { "GetMock", "visualmente" })
                if (fuente.Contains(prohibido, StringComparison.Ordinal)) pendientes.Add($"{Path.GetFileName(archivo)}: {prohibido}");
        }
        Assert.True(pendientes.Count == 0, string.Join(Environment.NewLine, pendientes));
    }

    [Fact]
    public async Task Providers_lista_la_api_y_cuenta_las_cuentas()
    {
        Handler.Responder("/api/emailproviders", ProvidersJson);
        Handler.Responder("/api/emailaccounts", CuentasJson);
        await IniciarSesionAsync(esSistema: true, idTenant: 1);

        var cut = Render<Providers>();

        cut.WaitForAssertion(() => Assert.Contains("SendGrid", cut.Markup));
        Assert.Equal(["1", "0"], cut.FindAll("[data-testid=provider-cuentas]").Select(e => e.TextContent.Trim()));
        Assert.NotEmpty(cut.FindAll("[data-testid=providers-nuevo]"));
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/emailproviders", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Admin_de_tenant_no_ve_crear_provider()
    {
        Handler.Responder("/api/emailproviders", ProvidersJson);
        Handler.Responder("/api/emailaccounts", CuentasJson);
        await IniciarSesionAsync(esSistema: false, idTenant: 2);

        var cut = Render<Providers>();

        cut.WaitForAssertion(() => Assert.Contains("SendGrid", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid=providers-nuevo]"));
        Assert.Empty(cut.FindAll("button[aria-label=Editar]"));
    }

    [Fact]
    public async Task Cuentas_por_tenant_de_admin_piden_su_tenant()
    {
        Handler.Responder("/api/emailaccounts", CuentasJson);
        Handler.Responder("/api/tenantemailaccounts/tenant/2",
            """[ { "id": 8, "idTenant": 2, "idEmailAccount": 1, "esPredeterminada": true, "activo": true } ]""");
        await IniciarSesionAsync(esSistema: false, idTenant: 2);

        var cut = Render<TenantEmailAccounts>();

        cut.WaitForAssertion(() => Assert.Contains("SMTP Global", cut.Markup));
        Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/tenantemailaccounts/tenant/2", StringComparison.Ordinal));
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("GET /api/tenants", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid=tenant-accounts-tenant]"));
    }

    [Fact]
    public async Task DominioTenantDialog_con_tenant_fijo_no_muestra_selector()
    {
        CargarCatalogos();
        await IniciarSesionAsync(esSistema: true, idTenant: 1);
        var cut = Render<TestHost>(p => p.AddChildContent<MudDialogProvider>());

        await Services.GetRequiredService<IDialogService>().ShowAsync<DominioTenantDialog>("Agregar Dominio",
            new DialogParameters<DominioTenantDialog> { { x => x.IdTenant, 1 } });

        cut.WaitForAssertion(() => cut.Find("[data-testid=boton-guardar-dialogo]"));
        // solo el campo Dominio: sin SelectorCatalogo de tenant
        var campos = cut.Find(".mud-dialog").QuerySelectorAll("input.mud-input-slot");
        Assert.Single(campos);
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("GET /api/tenants", StringComparison.Ordinal));

        cut.Find($"#{campos[0].Id}").Change(" Mail.S101.Test ");
        cut.Find("[data-testid=boton-guardar-dialogo]").Click();
        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones,
            p => p.StartsWith("POST /api/DominiosTenant", StringComparison.OrdinalIgnoreCase) && p.Contains("\"idTenant\":1") && p.Contains("mail.s101.test")));
    }
}
