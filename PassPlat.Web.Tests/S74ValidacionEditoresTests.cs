using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Pages.Tenants;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S74 · K4 — candado de la validación uniforme de los diálogos de formulario: <c>MudForm</c>,
/// <c>await _form.ValidateAsync()</c> al guardar y un botón primario que no se deshabilita por validación.
/// </summary>
public class S74ValidacionEditoresTests : WebTestBase
{
    private static readonly string[] Dialogos =
    [
        Path.Combine("Apps", "AppDialog.razor"),
        Path.Combine("Bloqueos", "BloqueoDialog.razor"),
        Path.Combine("ConfigApp", "ConfigAppDialog.razor"),
        Path.Combine("DominiosTenant", "DominioTenantDialog.razor"),
        Path.Combine("Tenants", "TenantDialog.razor"),
        Path.Combine("Grupos", "GrupoDialog.razor"),
        Path.Combine("Grupos", "AgregarMiembroGrupoDialog.razor"),
        Path.Combine("Email", "EmailAccountDialog.razor"),
        Path.Combine("Email", "ProviderDialog.razor"),
        Path.Combine("Email", "AssignAppDialog.razor"),
        Path.Combine("Email", "AssignTenantDialog.razor"),
        Path.Combine("EmailTemplates", "EmailTemplateDialog.razor"),
        Path.Combine("Federacion", "ConfProvIden", "ConfProvIdenDialog.razor"),
        Path.Combine("Federacion", "IdenExt", "IdenExtDialog.razor"),
        Path.Combine("Notificaciones", "NotificacionDialog.razor"),
        Path.Combine("PoliticasPwd", "PoliticaPwdDialog.razor"),
        Path.Combine("RolesPermisos", "AsignarUsuarioRolDialog.razor"),
        Path.Combine("RolesPermisos", "PermisoDialog.razor"),
        Path.Combine("RolesPermisos", "RolDialog.razor"),
        Path.Combine("RolesPermisos", "RolPoliticaPwdDialog.razor"),
        Path.Combine("RolesPermisos", "RolesHerenciaDialog.razor"),
        Path.Combine("Usuarios", "UsuarioDialog.razor")
    ];

    private static readonly Regex EtiquetaBoton = new("<BotonGuardarDialogo.*?/>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Pagina(string relativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "Pages", relativa));

    [Fact]
    public void Los_22_dialogos_usan_MudForm_y_validan_al_guardar()
    {
        // S101 · D6 — sale Tenants/DominioTenantDialog (unificado) y entran los 3 diálogos de Email
        Assert.Equal(22, Dialogos.Length);
        var pendientes = Dialogos
            .Where(d => !Pagina(d).Contains("<MudForm @ref=\"_form\"", StringComparison.Ordinal)
                     || !Pagina(d).Contains("await _form.ValidateAsync()", StringComparison.Ordinal))
            .ToList();
        Assert.True(pendientes.Count == 0, "Sin MudForm o sin ValidateAsync(): " + string.Join(", ", pendientes));
    }

    [Fact]
    public void Ningun_dialogo_usa_submitted_ni_CanSave()
    {
        var pendientes = new List<string>();
        foreach (var dialogo in Dialogos)
        {
            var fuente = Pagina(dialogo);
            foreach (var prohibido in new[] { "_submitted", "CanSave(", "PuedeGuardar(", "_cannotSave" })
                if (fuente.Contains(prohibido, StringComparison.Ordinal)) pendientes.Add($"{dialogo}: {prohibido}");
        }
        Assert.True(pendientes.Count == 0, string.Join(Environment.NewLine, pendientes));
    }

    [Fact]
    public void El_boton_primario_no_se_deshabilita_por_validacion()
    {
        var pendientes = new List<string>();
        foreach (var dialogo in Dialogos)
        {
            var etiqueta = EtiquetaBoton.Match(Pagina(dialogo));
            if (!etiqueta.Success) pendientes.Add($"{dialogo}: sin BotonGuardarDialogo");
            else if (etiqueta.Value.Contains("Deshabilitado=", StringComparison.Ordinal)) pendientes.Add($"{dialogo}: {etiqueta.Value}");
        }
        Assert.True(pendientes.Count == 0, string.Join(Environment.NewLine, pendientes));
    }

    [Fact]
    public async Task TenantDialog_crear_vacio_muestra_errores_y_no_envia()
    {
        var cut = await AbrirTenantDialogAsync(new DialogParameters<TenantDialog> { { x => x.IsEdit, false } });

        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("El código es requerido", cut.Markup);
            Assert.Contains("El nombre es requerido", cut.Markup);
        });
        Assert.DoesNotContain(Handler.Peticiones, p => p.StartsWith("POST /api/tenants", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TenantDialog_crear_valido_envia_post()
    {
        Handler.Responder("api/tenants", "{\"id\":9,\"codigo\":\"T9\",\"nombre\":\"N\",\"activo\":true}");
        var cut = await AbrirTenantDialogAsync(new DialogParameters<TenantDialog> { { x => x.IsEdit, false } });

        cut.FindAll("input.mud-input-slot")[0].Change("T9");
        cut.FindAll("input.mud-input-slot")[1].Change("N");
        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(Handler.Peticiones, p => p.StartsWith("POST /api/tenants", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TenantDialog_editar_envia_put()
    {
        Handler.Responder("api/tenants/9", "{\"id\":9,\"codigo\":\"T9\",\"nombre\":\"N\",\"activo\":true}");
        var tenant = new TenantDto { Id = 9, Codigo = "T9", Nombre = "N", Activo = true };
        var cut = await AbrirTenantDialogAsync(new DialogParameters<TenantDialog> { { x => x.IsEdit, true }, { x => x.Tenant, tenant } });

        cut.Find("[data-testid=boton-guardar-dialogo]").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(Handler.Peticiones, p => p.StartsWith("PUT /api/tenants/", StringComparison.Ordinal)));
    }

    [Fact]
    public void La_guia_documenta_la_validacion()
    {
        var guia = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "README-Estilos.md"));
        Assert.Contains("Validación (S74)", guia);
        Assert.Contains("MudForm", guia);
    }

    private async Task<IRenderedComponent<TestHost>> AbrirTenantDialogAsync(DialogParameters<TenantDialog> parametros)
    {
        await AutenticarAsync();
        var cut = Render<TestHost>(p => p.AddChildContent<MudDialogProvider>());
        var servicio = Services.GetRequiredService<IDialogService>();
        await servicio.ShowAsync<TenantDialog>("", parametros);
        cut.WaitForAssertion(() => cut.Find("[data-testid=boton-guardar-dialogo]"));
        return cut;
    }
}
