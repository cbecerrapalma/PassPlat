using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Pages.Tenants;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S73 · K6 — candado del patrón único de editor: los diálogos usan <c>TituloDialogo</c>,
/// <c>dialogo-campos</c> y <c>BotonGuardarDialogo</c> (CBP.Blazor.Mud.Dialogos); los detalles siguen
/// el patrón de <c>PermisoDetail</c> y la guía documenta el editor.
/// </summary>
public class S73EditoresTests : WebTestBase
{
    private static readonly string[] DialogosFormulario =
    [
        Path.Combine("Accesos", "AccesoDialog.razor"),
        Path.Combine("Apps", "AppDialog.razor"),
        Path.Combine("Bloqueos", "BloqueoDialog.razor"),
        Path.Combine("ConfigApp", "ConfigAppDialog.razor"),
        Path.Combine("ConfigTenants", "PepperVersionDialog.razor"),
        Path.Combine("DominiosTenant", "DominioTenantDialog.razor"),
        Path.Combine("Email", "AssignAppDialog.razor"),
        Path.Combine("Email", "AssignTenantDialog.razor"),
        Path.Combine("Email", "EmailAccountDialog.razor"),
        Path.Combine("Email", "ProviderDialog.razor"),
        Path.Combine("EmailTemplates", "EmailTemplateDialog.razor"),
        Path.Combine("Federacion", "ConfProvIden", "ConfProvIdenDialog.razor"),
        Path.Combine("Federacion", "IdenExt", "IdenExtDialog.razor"),
        Path.Combine("Grupos", "AgregarMiembroGrupoDialog.razor"),
        Path.Combine("Grupos", "GrupoDialog.razor"),
        Path.Combine("Notificaciones", "NotificacionDialog.razor"),
        Path.Combine("PoliticasPwd", "PoliticaPwdDialog.razor"),
        Path.Combine("RolesPermisos", "AsignarUsuarioRolDialog.razor"),
        Path.Combine("RolesPermisos", "PermisoDialog.razor"),
        Path.Combine("RolesPermisos", "PermisoDirectoDialog.razor"),
        Path.Combine("RolesPermisos", "RolDialog.razor"),
        Path.Combine("RolesPermisos", "RolPoliticaPwdDialog.razor"),
        Path.Combine("RolesPermisos", "RolesHerenciaDialog.razor"),
        Path.Combine("Tenants", "TenantDialog.razor"),
        Path.Combine("Usuarios", "UsuarioDialog.razor")
    ];

    private static readonly string[] DialogosSoloLectura =
    [
        Path.Combine("EmailTemplates", "HtmlPreviewDialog.razor"),
        Path.Combine("Federacion", "ProvIden", "ProvIdenDialog.razor")
    ];

    private static readonly string[] Detalles =
    [
        Path.Combine("Tenants", "TenantDetail.razor"),
        Path.Combine("Usuarios", "UsuarioDetail.razor"),
        Path.Combine("Grupos", "GrupoDetail.razor")
    ];

    private static readonly Regex RotuloSinTilde = new(
        "(Label|HelperText|ErrorText|Placeholder|Texto)=\"[^\"@]*\\b(Codigo|Descripcion|Aplicacion|Contrasena|Direccion|Version|unico|proposito)\\b",
        RegexOptions.CultureInvariant);

    private static IEnumerable<string> Dialogos => DialogosFormulario.Concat(DialogosSoloLectura);

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
    public void Los_27_dialogos_usan_TituloDialogo_y_no_Typo_h6()
    {
        // S101 · D6 — Tenants/DominioTenantDialog se unificó en DominiosTenant/DominioTenantDialog
        Assert.Equal(27, Dialogos.Count());
        var pendientes = Dialogos
            .Where(d => !Pagina(d).Contains("<TituloDialogo", StringComparison.Ordinal) || Pagina(d).Contains("Typo.h6", StringComparison.Ordinal))
            .ToList();
        Assert.True(pendientes.Count == 0, "Sin TituloDialogo o con Typo.h6: " + string.Join(", ", pendientes));
    }

    [Fact]
    public void Los_dialogos_de_formulario_usan_dialogo_campos_y_BotonGuardarDialogo()
    {
        Assert.Equal(25, DialogosFormulario.Length);
        var pendientes = DialogosFormulario
            .Where(d => !Pagina(d).Contains("dialogo-campos", StringComparison.Ordinal) || !Pagina(d).Contains("<BotonGuardarDialogo", StringComparison.Ordinal))
            .ToList();
        Assert.True(pendientes.Count == 0, "Fuera del patrón de formulario: " + string.Join(", ", pendientes));
    }

    [Fact]
    public void Ningun_dialogo_usa_mb3_ni_MudGrid()
    {
        var pendientes = Dialogos
            .Where(d => Pagina(d).Contains("mb-3", StringComparison.Ordinal) || Pagina(d).Contains("<MudGrid", StringComparison.Ordinal))
            .ToList();
        Assert.True(pendientes.Count == 0, "Con mb-3 o MudGrid: " + string.Join(", ", pendientes));
    }

    [Fact]
    public void Los_rotulos_visibles_llevan_tilde()
    {
        var pendientes = new List<string>();
        foreach (var archivo in Dialogos.Concat(Detalles))
        {
            var fuente = Pagina(archivo);
            foreach (Match m in RotuloSinTilde.Matches(fuente))
                pendientes.Add($"{archivo}: {m.Value}");
            if (fuente.Contains("Seleccioná", StringComparison.Ordinal))
                pendientes.Add($"{archivo}: Seleccioná");
        }
        Assert.Equal(30, Dialogos.Concat(Detalles).Count());
        Assert.True(pendientes.Count == 0, string.Join(Environment.NewLine, pendientes));
    }

    [Fact]
    public void Los_detalles_siguen_el_patron_sin_MudContainer_ni_MudPaper()
    {
        var pendientes = new List<string>();
        foreach (var archivo in Detalles)
        {
            var fuente = Pagina(archivo);
            foreach (var requerido in new[] { "class=\"iam-page\"", "page-title", "detail-tabs" })
                if (!fuente.Contains(requerido, StringComparison.Ordinal)) pendientes.Add($"{archivo}: falta {requerido}");
            foreach (var prohibido in new[] { "MudContainer", "MudPaper", "Typo.h4", "Typo.h5" })
                if (fuente.Contains(prohibido, StringComparison.Ordinal)) pendientes.Add($"{archivo}: usa {prohibido}");
        }
        Assert.True(pendientes.Count == 0, string.Join(Environment.NewLine, pendientes));
    }

    [Fact]
    public void La_guia_documenta_el_editor()
    {
        var guia = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "README-Estilos.md"));
        foreach (var texto in new[] { "TituloDialogo", "BotonGuardarDialogo", "dialogo-campos", "S73" })
            Assert.Contains(texto, guia);
    }

    [Fact]
    public async Task TenantDialog_renderiza_titulo_y_boton_compartidos()
    {
        await AutenticarAsync();
        var cut = Render<TestHost>(p => p.AddChildContent<MudDialogProvider>());
        var servicio = Services.GetRequiredService<IDialogService>();
        await servicio.ShowAsync<TenantDialog>("", new DialogParameters<TenantDialog> { { x => x.IsEdit, false } });

        cut.WaitForAssertion(() => Assert.Contains("Nuevo Tenant", cut.Find("[data-testid=titulo-dialogo]").TextContent));
        Assert.Contains("Crear", cut.Find("[data-testid=boton-guardar-dialogo]").TextContent);
    }
}
