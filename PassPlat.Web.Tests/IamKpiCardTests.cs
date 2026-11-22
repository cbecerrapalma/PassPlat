using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S55 · K3 (D4) — tarjeta KPI compartida: sin subtexto redundante y compacta
/// (2 por fila en celular).
/// </summary>
public class IamKpiCardTests : WebTestBase
{
    private IRenderedComponent<IamKpiCard> RenderKpi(string? subtexto = null) =>
        Render<IamKpiCard>(p => p
            .Add(x => x.Valor, "3")
            .Add(x => x.Label, "Usuarios Totales")
            .Add(x => x.Icon, Icons.Material.Filled.People)
            .Add(x => x.Subtexto, subtexto));

    [Fact]
    public void Muestra_el_valor_y_la_etiqueta()
    {
        var cut = RenderKpi();

        cut.WaitForAssertion(() => Assert.Contains("Usuarios Totales", cut.Markup));
        Assert.Contains("3", cut.Markup);
    }

    [Fact]
    public void No_pinta_subtexto_cuando_no_se_pasa_uno()
    {
        var cut = RenderKpi();

        var subtextos = cut.FindAll(".mud-typography-caption");
        Assert.Empty(subtextos);
    }

    [Fact]
    public void El_subtexto_se_pinta_solo_cuando_aporta_informacion()
    {
        var cut = RenderKpi(subtexto: "Tienen MFA activo");

        cut.WaitForAssertion(() => Assert.Contains("Tienen MFA activo", cut.Markup));
    }

    [Fact]
    public void La_tarjeta_no_emite_clases_de_columna()
    {
        var cut = RenderKpi();

        Assert.DoesNotContain("col-xs", cut.Markup);
        Assert.DoesNotContain("col-sm", cut.Markup);
        Assert.DoesNotContain("col-md", cut.Markup);
        Assert.DoesNotContain("col-lg", cut.Markup);
        Assert.Contains("iam-kpi-card", cut.Markup);
    }

    [Fact]
    public void La_tarjeta_no_usa_la_anchura_como_una_columna_de_MudBlazor()
    {
        var cut = RenderKpi();

        var clases = cut.Find("div[role='button']").GetAttribute("class") ?? "";

        Assert.DoesNotContain("col-", clases);
    }
}
