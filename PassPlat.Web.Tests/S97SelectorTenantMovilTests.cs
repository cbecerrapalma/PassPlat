using Bunit;
using MudBlazor;
using PassPlat.Web.Shared;

namespace PassPlat.Web.Tests.Tests.S97;

/// <summary>
/// S97 · D3 — el chip del tenant se ve también bajo 600 px (solo el icono); el nombre se oculta solo en xs.
/// </summary>
public class S97SelectorTenantMovilTests : WebTestBase
{
    private static void Selector(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b)
    {
        b.OpenComponent<MudPopoverProvider>(0);
        b.CloseComponent();
        b.OpenComponent<TenantSwitcher>(1);
        b.CloseComponent();
    }

    [Fact]
    public async Task Chip_VisibleEnMovil_ConNombreAccesible()
    {
        await AutenticarAsync(1, 1, "sistema");

        var chip = Render(Selector).Find(".tenant-switcher-chip");

        Assert.DoesNotContain("d-none", chip.ClassList);
        Assert.Equal("Cambiar de tenant", chip.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Etiqueta_OcultaSoloBajo600px()
    {
        await AutenticarAsync(1, 1, "sistema");

        var etiqueta = Render(Selector).Find(".tenant-switcher-label");

        Assert.Contains("d-none", etiqueta.ClassList);
        Assert.Contains("d-sm-inline", etiqueta.ClassList);
    }
}
