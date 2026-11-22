using System.Text.RegularExpressions;
using Bunit;
using PassPlat.Web.Layout;

namespace PassPlat.Web.Tests.Tests.S96;

/// <summary>
/// S96 · D1 y D3 — el panel de la campana flota sobre la página y muestra la hora tal como llega.
/// </summary>
public class S96CampanaTests : WebTestBase
{
    private const string Ruta = "/api/notificaciones/no-leidas/7";

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Regla(string css, string selector)
    {
        var m = Regex.Match(css, @"(?m)^" + Regex.Escape(selector) + @"\s*\{(?<cuerpo>[^}]*)\}");
        Assert.True(m.Success, $"No existe la regla {selector}");
        return m.Groups["cuerpo"].Value;
    }

    [Fact]
    public void Css_PanelDeLaCampana_EsAbsoluto()
    {
        var css = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "wwwroot", "css", "app.css"));

        var panel = Regla(css, ".appbar-notif-dropdown");
        Assert.Matches(@"position\s*:\s*absolute\s*;", panel);
        Assert.Matches(@"max-width\s*:\s*calc\(100vw - 24px\)\s*;", panel);
        Regla(css, ".mud-dark .appbar-notif-dropdown");
    }

    [Fact]
    public async Task Campana_MuestraLaHoraTalComoLlega()
    {
        await AutenticarAsync(7, 1, "sistema", "NOTIFICACIONES_VER");
        Handler.Responder(Ruta, """[{"id":1,"idUsuario":7,"idTenant":1,"tipoNotif":"INFO","asunto":"A1","fecCrea":"2026-11-02T22:49:00"}]""");

        var cut = Render<CampanaNotificaciones>(p => p.Add(x => x.Abierto, true));

        var esperada = new DateTime(2026, 11, 2, 22, 49, 0).ToString("g");
        cut.WaitForAssertion(() => Assert.Equal(esperada, cut.Find(".notif-time-inline").TextContent.Trim()));
    }
}
