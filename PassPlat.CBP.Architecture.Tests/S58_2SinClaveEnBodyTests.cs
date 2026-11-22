using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S58.2 · D1 — el contenedor de <c>@Body</c> en el layout no puede llevar <c>@key</c>.
/// Cuando la clave cambia, Blazor destruye la pagina y la vuelve a crear: su
/// <c>OnInitializedAsync</c> corre dos veces y cada pagina pide sus recursos a la API
/// dos veces. El reinicio del <c>ErrorBoundary</c> se resuelve con su <c>@ref</c> y
/// <c>Recover()</c> en el evento de navegacion, sin recrear la pagina.
/// </summary>
public sealed class S58_2SinClaveEnBodyTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string CarpetaLayout()
    {
        return Path.Combine(RaizDelRepo(), "PassPlat.Web", "Layout");
    }

    private static IEnumerable<string> ArchivosDelLayout()
    {
        return Directory.GetFiles(CarpetaLayout(), "*.razor", SearchOption.AllDirectories)
            .OrderBy(x => x);
    }

    /// <summary>
    /// Elemento que envuelve a <c>@Body</c>: desde la ultima apertura de etiqueta antes
    /// de <c>@Body</c> hasta el propio <c>@Body</c>.
    /// </summary>
    private static string ContenedorDeBody(string contenido)
    {
        var i = contenido.IndexOf("@Body", StringComparison.Ordinal);
        if (i < 0) return string.Empty;
        var antes = contenido[..i];
        var apertura = antes.LastIndexOf('<');
        return apertura < 0 ? string.Empty : antes[apertura..];
    }

    [Fact]
    public void S58_2_ningun_razor_de_Layout_usa_key_en_el_contenedor_de_Body()
    {
        var visitar = new List<string>();

        foreach (var archivo in ArchivosDelLayout())
        {
            var contenido = File.ReadAllText(archivo);
            if (!contenido.Contains("@Body", StringComparison.Ordinal)) continue;

            var contenedor = ContenedorDeBody(contenido);
            if (contenedor.Contains("@key", StringComparison.Ordinal))
            {
                visitar.Add($"{Path.GetFileName(archivo)} -> {contenedor.Trim()}");
            }
        }

        Assert.True(visitar.Count == 0,
            "El contenedor de @Body lleva @key: Blazor recrea la pagina y duplica sus llamadas a la API:\n  "
            + string.Join("\n  ", visitar));
    }

    [Fact]
    public void S58_2_el_layout_reinicia_el_ErrorBoundary_sin_recrear_la_pagina()
    {
        var mainLayout = Path.Combine(CarpetaLayout(), "MainLayout.razor");
        Assert.True(File.Exists(mainLayout), $"No se encontro {mainLayout}.");
        var contenido = File.ReadAllText(mainLayout);

        Assert.DoesNotContain("@key", contenido, StringComparison.Ordinal);
        Assert.Contains("ErrorBoundary", contenido, StringComparison.Ordinal);
        Assert.Contains("Recover()", contenido, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(contenido, @"_rutaActual"),
            "MainLayout mantiene el campo _rutaActual, que solo existia para la clave @key.");
    }
}
