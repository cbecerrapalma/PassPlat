using System.Text.RegularExpressions;

namespace PassPlat.Web.Tests;

/// <summary>
/// S67 · K4 (D4) — candados para que no vuelvan a aparecer los defectos de S67:
/// 1) Ningún .razor usa MudTab (RZ10012) — solo MudTabs y MudTabPanel.
/// 2) Usuarios/Index.razor importa el namespace de componentes de Usuarios.
/// 3) Las páginas de S67 no leen Canceled sin comprobar null (CS8602).
/// </summary>
public class S67EtiquetasRazorTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static List<(string Archivo, string Texto)> ArchivosRazor()
    {
        var raiz = RaizDelRepo();
        return Directory.GetFiles(Path.Combine(raiz, "PassPlat.Web"), "*.razor", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                      && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(p => (p.Replace($"{raiz}{Path.DirectorySeparatorChar}", "").Replace("\\", "/"), File.ReadAllText(p)))
            .ToList();
    }

    /// <summary>
    /// Ningún .razor de PassPlat.Web contiene <MudTab seguido de espacio o >, ni </MudTab>.
    /// MudTabs y MudTabPanel no deben contar.
    /// </summary>
    [Fact]
    public void Ningun_razor_usa_MudTab()
    {
        var conMudTab = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            // Busca <MudTab seguido de espacio o > (no <MudTabs ni <MudTabPanel)
            foreach (Match m in Regex.Matches(texto, @"<MudTab\b(?![a-zA-Z])"))
            {
                var siguiente = m.Index + m.Length;
                if (siguiente < texto.Length && (texto[siguiente] == ' ' || texto[siguiente] == '>'))
                    conMudTab.Add($"{archivo}:{m.Index}: {texto.Substring(Math.Max(0, m.Index - 10), Math.Min(50, texto.Length - m.Index)).Replace("\n", " ").Trim()}");
            }
            // Busca </MudTab>
            if (texto.Contains("</MudTab>", StringComparison.Ordinal))
            {
                var idx = texto.IndexOf("</MudTab>", StringComparison.Ordinal);
                conMudTab.Add($"{archivo}:{idx}: </MudTab> encontrado");
            }
        }

        Assert.Empty(conMudTab);
    }

    /// <summary>
    /// Usuarios/Index.razor contiene @using PassPlat.Web.Pages.Usuarios.Components
    /// </summary>
    [Fact]
    public void Usuarios_Index_importa_los_componentes_de_Usuarios()
    {
        var raiz = RaizDelRepo();
        var ruta = Path.Combine(raiz, "PassPlat.Web", "Pages", "Usuarios", "Index.razor");
        var texto = File.ReadAllText(ruta);

        Assert.Contains("@using PassPlat.Web.Pages.Usuarios.Components", texto);
    }

    /// <summary>
    /// Ni Pages/Tenants/TenantDetail.razor ni Pages/Usuarios/Index.razor contienen !result.Canceled.
    /// </summary>
    [Fact]
    public void Paginas_S67_no_leen_Canceled_sin_comprobar_null()
    {
        var raiz = RaizDelRepo();
        var archivos = new[]
        {
            Path.Combine(raiz, "PassPlat.Web", "Pages", "Tenants", "TenantDetail.razor"),
            Path.Combine(raiz, "PassPlat.Web", "Pages", "Usuarios", "Index.razor")
        };
        var conCanceled = new List<string>();

        foreach (var ruta in archivos)
        {
            var texto = File.ReadAllText(ruta);
            foreach (Match m in Regex.Matches(texto, @"!result\.Canceled"))
            {
                conCanceled.Add($"{ruta.Replace($"{raiz}{Path.DirectorySeparatorChar}", "").Replace("\\", "/")}:{m.Index}: !result.Canceled encontrado");
            }
        }

        Assert.Empty(conCanceled);
    }
}