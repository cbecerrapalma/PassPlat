using System.Text.RegularExpressions;

namespace PassPlat.Web.Tests;

/// <summary>
/// S72 — La rueda del ratón sobre una tabla o una lista de la página desplaza la página:
/// <c>overscroll-behavior: contain</c> queda solo en el menú lateral y en las listas desplegables.
/// </summary>
public class S72ScrollListasTests
{
    private static readonly Regex Comentarios = new(@"/\*.*?\*/", RegexOptions.Singleline);
    private static readonly Regex Reglas = new(@"([^{}]+)\{([^{}]*)\}");
    private static readonly Regex Contain = new(@"overscroll-behavior(-y)?:(contain|none)");
    private static readonly Regex Espacios = new(@"\s+");

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    /// <summary>Selectores de cada regla de <c>app.css</c> e <c>iam-pages.css</c> que contiene la rueda vertical.</summary>
    private static List<string[]> ReglasContain()
    {
        var css = Path.Combine(RaizDelRepo(), "PassPlat.Web", "wwwroot", "css");
        var reglas = new List<string[]>();

        foreach (var archivo in new[] { "app.css", "iam-pages.css" })
        {
            var texto = Comentarios.Replace(File.ReadAllText(Path.Combine(css, archivo)), string.Empty);
            foreach (Match regla in Reglas.Matches(texto))
            {
                if (!Contain.IsMatch(Espacios.Replace(regla.Groups[2].Value, string.Empty)))
                    continue;

                reglas.Add(regla.Groups[1].Value
                    .Split(',')
                    .Select(selector => Espacios.Replace(selector, " ").Trim())
                    .Where(selector => selector.Length > 0)
                    .ToArray());
            }
        }

        return reglas;
    }

    [Fact]
    public void Ninguna_regla_contain_incluye_tablas()
    {
        var prohibidos = new[] { "mud-table-container", "table-panel", "table-wrap", "mud-table" };

        var pendientes = ReglasContain()
            .SelectMany(selectores => selectores)
            .Where(selector => prohibidos.Any(p => selector.Contains(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(pendientes);
    }

    [Fact]
    public void Mud_list_solo_se_contiene_dentro_de_popover()
    {
        var pendientes = ReglasContain()
            .SelectMany(selectores => selectores)
            .Where(selector => selector.Contains(".mud-list", StringComparison.Ordinal))
            .Where(selector => !selector.StartsWith(".mud-popover ", StringComparison.Ordinal)
                && !selector.StartsWith(".login-card ", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(pendientes);
    }

    [Fact]
    public void El_menu_lateral_y_las_listas_desplegables_siguen_contenidos()
    {
        Assert.Contains(ReglasContain(), selectores =>
            selectores.Contains(".mud-drawer") && selectores.Contains(".mud-popover .mud-list"));
    }

    [Fact]
    public void La_guia_prohibe_contain_en_tablas()
    {
        var guia = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "README-Estilos.md"));

        Assert.Contains("overscroll-behavior: contain", guia, StringComparison.Ordinal);
        Assert.Contains("S72", guia, StringComparison.Ordinal);
    }
}
