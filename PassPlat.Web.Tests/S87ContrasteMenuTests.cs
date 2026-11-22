using System.Globalization;
using System.Text.RegularExpressions;

namespace PassPlat.Web.Tests;

/// <summary>
/// S87 — Los textos del cajón (#1e293b) llegan a contraste AA (4,5:1) y sus iconos a 3:1, sin opacidades que se
/// sumen al color; el menú solo tiene reglas dentro de <c>.mud-drawer</c> y no queda CSS de etiquetas de sección.
/// </summary>
public class S87ContrasteMenuTests
{
    private const string Grupo = ".mud-drawer .mud-navmenu .mud-nav-group > .mud-nav-link";

    private static readonly Regex Comentarios = new(@"/\*.*?\*/", RegexOptions.Singleline);
    private static readonly Regex Reglas = new(@"([^{}]+)\{([^{}]*)\}");
    private static readonly Regex Espacios = new(@"\s+");
    private static readonly Regex Blanco = new(@"^color:rgba\(255,255,255,([0-9.]+)\)$");

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

    private static string[] DeclaracionesDe(string selector) =>
        ReglasCss()
            .Where(regla => regla.Selectores.Contains(selector))
            .SelectMany(regla => regla.Declaraciones)
            .ToArray();

    /// <summary>Opacidad del último <c>color: rgba(255,255,255,a)</c> del selector.</summary>
    private static double AlfaDe(string selector)
    {
        var colores = DeclaracionesDe(selector)
            .Select(declaracion => Blanco.Match(declaracion))
            .Where(coincidencia => coincidencia.Success)
            .ToList();

        Assert.NotEmpty(colores);
        return double.Parse(colores[^1].Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Contraste WCAG del blanco con opacidad <paramref name="alfa"/> sobre el fondo del cajón (#1e293b).</summary>
    private static double ContrasteSobreCajon(double alfa)
    {
        double[] fondo = [0x1e, 0x29, 0x3b];
        var texto = fondo.Select(canal => canal + (255 - canal) * alfa).ToArray();
        return (Luminancia(texto) + 0.05) / (Luminancia(fondo) + 0.05);

        static double Lineal(double canal)
        {
            canal /= 255;
            return canal <= 0.03928 ? canal / 12.92 : Math.Pow((canal + 0.055) / 1.055, 2.4);
        }

        static double Luminancia(double[] rgb) =>
            0.2126 * Lineal(rgb[0]) + 0.7152 * Lineal(rgb[1]) + 0.0722 * Lineal(rgb[2]);
    }

    [Theory]
    [InlineData(Grupo)]
    [InlineData(".drawer-user-role")]
    [InlineData(".mud-dark .drawer-user-role")]
    [InlineData(".drawer-footer-text")]
    public void Los_textos_del_cajon_llegan_a_AA(string selector)
    {
        var contraste = ContrasteSobreCajon(AlfaDe(selector));

        Assert.True(contraste >= 4.5, $"{selector}: {contraste:F2}:1");
    }

    [Theory]
    [InlineData(Grupo + " .mud-nav-link-icon")]
    [InlineData(Grupo + " .mud-nav-link-expand-icon")]
    public void Los_iconos_del_grupo_llegan_a_3_a_1(string selector)
    {
        var contraste = ContrasteSobreCajon(AlfaDe(selector));

        Assert.True(contraste >= 3.0, $"{selector}: {contraste:F2}:1");
    }

    [Fact]
    public void Ninguna_regla_del_menu_baja_la_opacidad()
    {
        var conOpacidad = ReglasCss()
            .Where(regla => regla.Selectores.Any(selector => selector.Contains(".mud-navmenu", StringComparison.Ordinal)))
            .Where(regla => regla.Declaraciones.Any(declaracion => declaracion.StartsWith("opacity:", StringComparison.Ordinal)))
            .SelectMany(regla => regla.Selectores)
            .ToList();

        Assert.Empty(conOpacidad);
    }

    [Fact]
    public void El_menu_solo_tiene_reglas_dentro_del_cajon()
    {
        var sueltas = ReglasCss()
            .SelectMany(regla => regla.Selectores)
            .Where(selector => selector.Contains(".mud-navmenu", StringComparison.Ordinal)
                && !selector.StartsWith(".mud-drawer", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(sueltas);
    }

    [Fact]
    public void No_queda_css_de_etiquetas_de_seccion()
    {
        Assert.DoesNotContain(
            ReglasCss().SelectMany(regla => regla.Selectores),
            selector => selector.Contains(".nav-section-label", StringComparison.Ordinal));
    }

    [Fact]
    public void El_cajon_conserva_lo_que_daba_la_regla_suelta()
    {
        Assert.Contains("position:relative", DeclaracionesDe(".mud-drawer .mud-navmenu .mud-nav-link"));
        Assert.Contains("padding:4px8px", DeclaracionesDe(".mud-drawer .mud-drawer-content > .mud-navmenu"));
    }
}
