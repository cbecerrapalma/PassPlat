using System.Text.RegularExpressions;

namespace PassPlat.Web.Tests;

/// <summary>
/// S55 · K6 (D7) y K3 (D4/U13) — comprobaciones de estilo sobre el codigo de la Web:
/// todo boton de icono tiene nombre accesible, las migas de pan son enlaces reales
/// y no queda ningun color hex suelto en los .razor (D2).
/// </summary>
public class AccesibilidadTests
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

    [Fact]
    public void Todos_los_botones_de_icono_tienen_aria_label()
    {
        var sinNombre = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            foreach (var etiqueta in EtiquetasDeComponente(texto, "MudIconButton"))
            {
                if (!etiqueta.Contains("aria-label", StringComparison.OrdinalIgnoreCase))
                    sinNombre.Add($"{archivo}: {etiqueta.Replace('\n', ' ').Trim()}");
            }
        }

        Assert.Empty(sinNombre);
    }

    /// <summary>
    /// Extrae las etiquetas de un componente leyendo el fin de la etiqueta sin
    /// confundirse con el cierre de una expresion lambda dentro de un atributo.
    /// No confundirse con un componente que empieza por el mismo texto
    /// (por ejemplo MudMenu y MudMenuItem).
    /// </summary>
    private static List<string> EtiquetasDeComponente(string texto, string componente)
    {
        var etiquetas = new List<string>();
        var marca = $"<{componente}";
        var indice = 0;
        while (true)
        {
            var inicio = texto.IndexOf(marca, indice, StringComparison.Ordinal);
            if (inicio < 0) break;

            var siguiente = inicio + marca.Length;
            if (siguiente < texto.Length && char.IsLetter(texto[siguiente]))
            {
                indice = siguiente;
                continue;
            }

            var i = siguiente;
            var enComillas = false;
            while (i < texto.Length)
            {
                var c = texto[i];
                if (c == '"') enComillas = !enComillas;
                else if (c == '>' && !enComillas) break;
                i++;
            }
            etiquetas.Add(texto.Substring(inicio, Math.Min(i + 1, texto.Length) - inicio));
            indice = i + 1;
        }
        return etiquetas;
    }

    [Fact]
    public void Las_migas_de_pan_son_enlaces_reales_y_no_usan_onclick()
    {
        var conOnClick = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            foreach (Match m in Regex.Matches(texto, "<MudBreadcrumbs\\b[^>]*>"))
            {
                if (m.Value.Contains("OnClick", StringComparison.OrdinalIgnoreCase))
                    conOnClick.Add(archivo);
            }
            if (texto.Contains("<MudBreadcrumbItem", StringComparison.Ordinal))
                conOnClick.Add($"{archivo}: queda <MudBreadcrumbItem>");
        }

        Assert.Empty(conOnClick);
    }

    [Fact]
    public void No_queda_ningun_color_hex_suelto_en_los_razor()
    {
        var conColor = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            // Las entidades HTML (&amp;#171;) no son colores: se excluyen.
            foreach (Match m in Regex.Matches(texto, "(?<!&)#[0-9A-Fa-f]{3,8}\\b"))
                conColor.Add($"{archivo}: {m.Value}");
        }

        Assert.Empty(conColor);
    }

    [Fact]
    public void El_login_usa_el_tema_compartido_de_la_app()
    {
        var raiz = RaizDelRepo();
        var login = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "LoginLayout.razor"));

        Assert.Contains("AppTheme.Theme", login);
        Assert.DoesNotContain("<MudThemeProvider />", login);
    }

    /// <summary>
    /// S55.1 · K3 (D3) — un menu que se abre con un boton de solo icono necesita
    /// nombre accesible (aria-label, label o un activador con texto).
    /// </summary>
    [Fact]
    public void Los_menus_con_icono_tienen_nombre_accesible()
    {
        var sinNombre = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            foreach (var etiqueta in EtiquetasDeComponente(texto, "MudMenu"))
            {
                if (!etiqueta.Contains("Icon=", StringComparison.OrdinalIgnoreCase))
                    continue;

                var tieneNombre = etiqueta.Contains("AriaLabel", StringComparison.OrdinalIgnoreCase)
                    || etiqueta.Contains("Label=", StringComparison.OrdinalIgnoreCase)
                    || texto.Contains("ActivatorContent", StringComparison.Ordinal);

                if (!tieneNombre)
                    sinNombre.Add($"{archivo}: {etiqueta.Replace('\n', ' ').Trim()}");
            }
        }

        Assert.Empty(sinNombre);
    }

    /// <summary>
    /// S55.1 · K2 (D2) — todas las tarjetas KPI viven dentro de un contenedor .kpi-row
    /// y no queda ninguna rejilla antigua de MudBlazor para los KPI.
    /// </summary>
    [Fact]
    public void Las_tarjetas_kpi_estan_dentro_de_un_contenedor_kpi_row()
    {
        var fuera = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            if (!texto.Contains("<IamKpiCard", StringComparison.Ordinal))
                continue;

            var bloques = BloquesKpiRow(texto);
            foreach (Match m in Regex.Matches(texto, "<IamKpiCard"))
            {
                if (!bloques.Any(b => m.Index >= b.Inicio && m.Index <= b.Fin))
                    fuera.Add($"{archivo}: tarjeta KPI fuera del contenedor .kpi-row");
            }
        }

        Assert.Empty(fuera);
    }

    [Fact]
    public void No_queda_ninguna_rejilla_antigua_de_kpi()
    {
        var conGrid = new List<string>();
        foreach (var (archivo, texto) in ArchivosRazor())
        {
            if (texto.Contains("mud-grid-spacing-3", StringComparison.Ordinal))
                conGrid.Add(archivo);
        }

        Assert.Empty(conGrid);
    }

    /// <summary>
    /// S55.1 · K2 (D2) — la rejilla de KPI es una sola: dos columnas por debajo de
    /// 600 px y auto-fit a partir de 600 px, sin depender de .iam-page.
    /// </summary>
    [Fact]
    public void La_rejilla_de_kpi_define_dos_columnas_en_celular()
    {
        var raiz = RaizDelRepo();
        var css = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "wwwroot", "css", "iam-pages.css"));

        Assert.Contains("repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("@media (min-width: 600px)", css);
        Assert.Contains("repeat(auto-fit, minmax(180px, 1fr))", css);
        Assert.DoesNotContain(".iam-page .kpi-row", css);
    }

    /// <summary>Rangos de los contenedores <c>&lt;div class="kpi-row"&gt;</c> de un archivo.</summary>
    private static List<(int Inicio, int Fin)> BloquesKpiRow(string texto)
    {
        var bloques = new List<(int, int)>();
        var indice = 0;

        while (true)
        {
            var marca = texto.IndexOf("class=\"kpi-row", indice, StringComparison.Ordinal);
            if (marca < 0) break;

            var apertura = texto.LastIndexOf('<', marca);
            var pos = apertura;
            var profundidad = 0;
            var fin = -1;

            while (pos < texto.Length)
            {
                var abre = texto.IndexOf("<div", pos, StringComparison.Ordinal);
                var cierra = texto.IndexOf("</div>", pos, StringComparison.Ordinal);
                if (cierra < 0) break;

                if (abre >= 0 && abre < cierra)
                {
                    profundidad++;
                    pos = abre + 4;
                }
                else
                {
                    profundidad--;
                    pos = cierra + 6;
                    if (profundidad == 0)
                    {
                        fin = pos;
                        break;
                    }
                }
            }

            if (fin > 0) bloques.Add((apertura, fin));
            indice = marca + 1;
        }

        return bloques;
    }

    /// <summary>
    /// S59 · K2 (D3) — el boton de «abrir lista» del <c>MudAutocomplete</c> de
    /// <c>SelectorCatalogo</c> debe tener nombre accesible: antes salia sin
    /// <c>aria-label</c> (P3).
    /// </summary>
    [Fact]
    public void El_selector_de_catalogo_nombra_el_boton_de_abrir_lista()
    {
        var raiz = RaizDelRepo();
        var ruta = Path.Combine(raiz, "PassPlat.Web", "Shared", "SelectorCatalogo.razor");
        var texto = File.ReadAllText(ruta);

        Assert.Contains("AdornmentAriaLabel", texto);
        Assert.Contains("Abrir lista de", texto);
    }

    /// <summary>
    /// S59 · K2 (D4) — <c>BloqueoDialog</c> pide los tipos de bloqueo por la ruta real
    /// del controlador (<c>api/[controller]</c>): antes pedia <c>api/tipos-bloqueo</c> y
    /// la API respondia 404 (P4).
    /// </summary>
    [Fact]
    public void El_dialogo_de_bloqueo_pide_los_tipos_por_la_ruta_real()
    {
        var raiz = RaizDelRepo();
        var ruta = Path.Combine(raiz, "PassPlat.Web", "Pages", "Bloqueos", "BloqueoDialog.razor");
        var texto = File.ReadAllText(ruta);

        Assert.Contains("api/TiposBloqueo", texto);
        Assert.DoesNotContain("api/tipos-bloqueo", texto);
    }
}
