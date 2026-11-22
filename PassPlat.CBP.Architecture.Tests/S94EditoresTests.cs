using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S94 · P21/P26/P44 — las listas con ServerData repintan al terminar la carga (sin overlay colgado),
/// PoliticaPwdDialog abre con los obligatorios a la vista, la cuenta SMTP no se autocompleta con la
/// credencial de inicio de sesión, la cabecera de página se parte en móvil y contar-no-leidas responde
/// con el número.
/// </summary>
public sealed class S94EditoresTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(params string[] partes) =>
        File.ReadAllText(Path.Combine([RaizDelRepo(), .. partes]));

    [Fact]
    public void S94_Listas_con_ServerData_y_overlay_repintan_al_terminar_la_carga()
    {
        var raiz = RaizDelRepo();
        var paginas = Directory.GetFiles(Path.Combine(raiz, "PassPlat.Web", "Pages"), "*.razor", SearchOption.AllDirectories)
            .Select(f => (Ruta: Path.GetRelativePath(raiz, f), Texto: File.ReadAllText(f)))
            .Where(p => p.Texto.Contains("ServerData=") && p.Texto.Contains("<MudOverlay Visible=\"@_loading\""))
            .ToList();

        Assert.True(paginas.Count >= 4, $"Se esperaban al menos 4 listas con ServerData y overlay; hay {paginas.Count}");
        var sinRepintar = paginas
            .Where(p => !Regex.IsMatch(p.Texto, @"_loading = false;\s*StateHasChanged\(\);"))
            .Select(p => p.Ruta)
            .ToList();
        Assert.Empty(sinRepintar);
    }

    [Fact]
    public void S94_PoliticaPwdDialog_abre_el_panel_General_con_los_obligatorios()
    {
        var fuente = Leer("PassPlat.Web", "Pages", "PoliticasPwd", "PoliticaPwdDialog.razor");

        Assert.Contains("<MudExpansionPanel Text=\"General\" Expanded=\"true\">", fuente);
    }

    [Fact]
    public void S94_EmailAccountDialog_no_autocompleta_la_credencial_SMTP()
    {
        var fuente = Leer("PassPlat.Web", "Pages", "Email", "EmailAccountDialog.razor");

        Assert.Matches(new Regex(@"@bind-Value=""_usuario""[^>]*autocomplete=""off"""), fuente);
        Assert.Matches(new Regex(@"@bind-Value=""_password""[^>]*autocomplete=""new-password"""), fuente);
    }

    [Fact]
    public void S94_Cabecera_de_pagina_se_parte_en_movil()
    {
        var css = Leer("PassPlat.Web", "wwwroot", "css", "iam-pages.css");

        Assert.Matches(new Regex(@"@media \(max-width: 599px\) \{\s*\.iam-page \.page-title-row,\s*\.iam-page \.page-title-row > div,\s*\.iam-page \.page-actions \{ flex-wrap: wrap; \}"), css);
    }

    [Fact]
    public void S94_NoLeidas_responde_con_FromResultQuery_y_contar_no_leidas_ya_no_existe()
    {
        var fuente = Leer("PassPlat.WebAPI", "Controllers", "NotificacionesController.cs");

        // S135 · D1 — contar-no-leidas se eliminó (sin uso); no-leidas conserva FromResultQuery
        Assert.Contains("FromResultQuery(await _service.ObtenerNoLeidasAsync(idUsuario, User.AlcanceTenantDe(_tenantContext, idUsuario), ct))", fuente);
        Assert.DoesNotContain("contar-no-leidas", fuente);
        Assert.DoesNotContain("Ok(await _service.", fuente);
    }
}
