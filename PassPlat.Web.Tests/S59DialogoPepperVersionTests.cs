using MudBlazor;
using PassPlat.Web.Shared;

namespace PassPlat.Web.Tests;

/// <summary>
/// S59 · K6 (D10) — la barra superior a 375 px y el tamaño del diálogo de Pepper Version,
/// que en S58.2 quedo sin medir porque la pagina no tiene registros.
/// </summary>
public class S59DialogoPepperVersionTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public void El_dialogo_de_pepper_version_usa_el_tamano_compacto()
    {
        var raiz = RaizDelRepo();
        var pagina = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "ConfigTenants", "Index.razor"));

        Assert.Contains("ShowAsync<PepperVersionDialog>", pagina);
        Assert.Contains("OpcionesDialogo.Dialogo(TamanoDialogo.Compacto)", pagina);
        // Compacto = Small: el ancho maximo de MudBlazor mas el minimo del CSS propio.
        Assert.Equal(MudBlazor.MaxWidth.Small, OpcionesDialogo.MaxWidthDe(TamanoDialogo.Compacto));
    }

    [Fact]
    public void Los_dialogos_de_formulario_no_bajan_de_560_px_en_escritorio()
    {
        var raiz = RaizDelRepo();
        var css = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "wwwroot", "css", "app.css"));

        // Ancho minimo del dialogo: 560 px en escritorio y 343 px en movil (100vw - 32 px).
        Assert.Contains("min-width: min(560px, calc(100vw - 32px))", css);
    }
}
