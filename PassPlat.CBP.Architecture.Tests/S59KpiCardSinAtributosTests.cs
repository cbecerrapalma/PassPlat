using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S59 · K3 (D5) — <c>IamKpiCard</c> ya no decide columnas: las decide la rejilla
/// <c>.kpi-row</c>. Por eso se eliminaron los parametros <c>Xs</c>/<c>Sm</c>/<c>Md</c>/
/// <c>Lg</c> y ningun <c>.razor</c> puede volver a pasarlos.
/// </summary>
public sealed class S59KpiCardSinAtributosTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static IEnumerable<string> ArchivosDeLaWeb()
    {
        var raiz = Path.Combine(RaizDelRepo(), "PassPlat.Web");
        return Directory.GetFiles(raiz, "*.razor", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(x => x);
    }

    [Fact]
    public void S59_ningun_razor_pasa_Xs_Sm_Md_Lg_a_IamKpiCard()
    {
        var infractores = new List<string>();

        foreach (var archivo in ArchivosDeLaWeb())
        {
            var texto = File.ReadAllText(archivo);
            foreach (Match etiqueta in Regex.Matches(texto, "<IamKpiCard[^>]*?>"))
            {
                if (Regex.IsMatch(etiqueta.Value, @"\s(Xs|Sm|Md|Lg)="))
                    infractores.Add($"{Path.GetFileName(archivo)} -> {etiqueta.Value.Replace('\n', ' ').Trim()}");
            }
        }

        Assert.True(infractores.Count == 0,
            "IamKpiCard ya no usa Xs/Sm/Md/Lg (el ancho lo decide .kpi-row), pero se le siguen pasando:\n  "
            + string.Join("\n  ", infractores));
    }

    [Fact]
    public void S59_IamKpiCard_no_declara_los_parametros_de_columna()
    {
        var ruta = Path.Combine(RaizDelRepo(), "PassPlat.Web", "Shared", "IamKpiCard.razor");
        Assert.True(File.Exists(ruta), $"No se encontro {ruta}.");
        var texto = File.ReadAllText(ruta);

        foreach (var atributo in new[] { "Xs", "Sm", "Md", "Lg" })
        {
            Assert.False(Regex.IsMatch(texto, $@"\[Parameter\]\s+public\s+\w+\??\s+{atributo}\s*\{{"),
                $"IamKpiCard todavia declara el parametro {atributo}.");
        }
    }
}
