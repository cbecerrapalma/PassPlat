using System.Text.RegularExpressions;

namespace PassPlat.Web.Tests;

/// <summary>
/// S82 — Con todos los grupos abiertos, la rueda sobre el menú lateral llega hasta la última opción:
/// MudBlazor envuelve el contenido del cajón en <c>.mud-drawer-content</c>, así que la lista de enlaces
/// desplaza en <c>.mud-drawer .mud-drawer-content &gt; .mud-navmenu</c> y no como hijo directo de <c>.mud-drawer</c>.
/// </summary>
public class S82MenuLateralTests
{
    private const string ListaDelMenu = ".mud-drawer .mud-drawer-content > .mud-navmenu";

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

    /// <summary>Declaración sin espacios ni <c>!important</c>, para comparar.</summary>
    private static string Normalizar(string declaracion) =>
        Espacios.Replace(declaracion, string.Empty).Replace("!important", string.Empty, StringComparison.Ordinal);

    /// <summary>Selectores y declaraciones normalizadas de cada regla de <c>app.css</c> e <c>iam-pages.css</c>.</summary>
    private static List<(string[] Selectores, string[] Declaraciones)> ReglasCss()
    {
        var css = Path.Combine(RaizDelRepo(), "PassPlat.Web", "wwwroot", "css");
        var reglas = new List<(string[] Selectores, string[] Declaraciones)>();

        foreach (var archivo in new[] { "app.css", "iam-pages.css" })
        {
            var texto = Comentarios.Replace(File.ReadAllText(Path.Combine(css, archivo)), string.Empty);
            foreach (Match regla in Reglas.Matches(texto))
            {
                var selectores = regla.Groups[1].Value
                    .Split(',')
                    .Select(selector => Espacios.Replace(selector, " ").Trim())
                    .Where(selector => selector.Length > 0)
                    .ToArray();
                var declaraciones = regla.Groups[2].Value
                    .Split(';')
                    .Select(Normalizar)
                    .Where(declaracion => declaracion.Length > 0)
                    .ToArray();

                reglas.Add((selectores, declaraciones));
            }
        }

        return reglas;
    }

    [Fact]
    public void La_lista_del_menu_desplaza_en_vertical()
    {
        var esperadas = new[] { "overflow-y: auto", "min-height: 0", "flex: 1 1 auto" }.Select(Normalizar).ToArray();

        Assert.Contains(ReglasCss(), regla =>
            regla.Selectores.Contains(ListaDelMenu)
            && esperadas.All(esperada => regla.Declaraciones.Contains(esperada)));
    }

    [Fact]
    public void Ningun_selector_espera_el_menu_como_hijo_del_cajon()
    {
        var prohibidos = new[] { ".mud-drawer > .mud-navmenu", ".mud-drawer::-webkit-scrollbar" };

        var pendientes = ReglasCss()
            .SelectMany(regla => regla.Selectores)
            .Where(selector => prohibidos.Any(p => selector.Contains(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(pendientes);
    }

    [Fact]
    public void La_lista_del_menu_contiene_la_rueda()
    {
        Assert.Contains(ReglasCss(), regla =>
            regla.Declaraciones.Any(declaracion => Contain.IsMatch(declaracion))
            && regla.Selectores.Contains(".mud-drawer")
            && regla.Selectores.Contains(ListaDelMenu));
    }

    [Fact]
    public void La_guia_explica_el_contenedor_del_menu()
    {
        var guia = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "README-Estilos.md"));

        Assert.Contains(".mud-drawer-content > .mud-navmenu", guia, StringComparison.Ordinal);
        Assert.Contains("S82", guia, StringComparison.Ordinal);
    }
}
