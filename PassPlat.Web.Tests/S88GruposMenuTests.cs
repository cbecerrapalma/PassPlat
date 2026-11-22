using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using NavMenu = PassPlat.Web.Layout.NavMenu;

namespace PassPlat.Web.Tests;

/// <summary>
/// S88 — el encabezado del grupo abre y cierra el grupo, el grupo de la página activa se abre solo y lo que el
/// usuario abre o cierra se guarda en <c>localStorage</c> (clave <c>menu_grupos</c>).
/// </summary>
public class S88GruposMenuTests : WebTestBase
{
    private const string RutaMenu = "/api/modulos/menu";
    private const string Clave = "menu_grupos";
    private const string Encabezado = ".mud-drawer .mud-navmenu .mud-nav-group > .mud-nav-link";

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    private static Dictionary<string, bool> Grupos(IRenderedComponent<NavMenu> cut) =>
        cut.FindComponents<MudNavGroup>().ToDictionary(g => g.Instance.Title ?? "", g => g.Instance.GetState(x => x.Expanded));

    private async Task<IRenderedComponent<NavMenu>> RenderEnAsync(string ruta)
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        Handler.Responder(RutaMenu, S83MenuDePrueba.Json());
        Nav.NavigateTo(ruta);
        var cut = Render<NavMenu>();
        cut.WaitForAssertion(() => Assert.Equal(3, Grupos(cut).Count));
        return cut;
    }

    [Fact]
    public async Task El_grupo_de_la_pagina_activa_se_abre_solo()
    {
        var cut = await RenderEnAsync("/auditoria");

        cut.WaitForAssertion(() => Assert.True(Grupos(cut)["Auditoría"]));
        Assert.False(Grupos(cut)["IAM"]);
        Assert.False(Grupos(cut)["Correos"]);
    }

    [Fact]
    public async Task Al_navegar_se_abre_el_grupo_de_la_nueva_pagina()
    {
        var cut = await RenderEnAsync("/");
        Assert.DoesNotContain(true, Grupos(cut).Values);

        await cut.InvokeAsync(() => Nav.NavigateTo("/email/providers"));

        cut.WaitForAssertion(() => Assert.True(Grupos(cut)["Correos"]));
        Assert.False(Grupos(cut)["IAM"]);
    }

    [Fact]
    public async Task Abrir_un_grupo_lo_guarda_en_el_navegador()
    {
        var cut = await RenderEnAsync("/auditoria");
        var iam = cut.FindComponents<MudNavGroup>().Single(g => g.Instance.Title == "IAM");

        iam.Find("button").Click();

        cut.WaitForAssertion(() => Assert.True(Grupos(cut)["IAM"]));
        var guardado = JSInterop.Invocations["localStorage.setItem"]
            .Where(i => Equals(i.Arguments[0], Clave))
            .Select(i => (string)i.Arguments[1]!)
            .Last();
        Assert.Contains("\"IAM\":true", guardado);
        Assert.Contains("\"AUDITORIA\":true", guardado);
    }

    [Fact]
    public async Task El_estado_guardado_se_aplica_al_cargar()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Clave).SetResult("{\"EMAIL\":true,\"IAM\":false}");

        var cut = await RenderEnAsync("/");

        cut.WaitForAssertion(() => Assert.True(Grupos(cut)["Correos"]));
        Assert.False(Grupos(cut)["IAM"]);
        Assert.False(Grupos(cut)["Auditoría"]);
    }

    [Fact]
    public async Task Un_valor_guardado_ilegible_no_rompe_el_menu()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Clave).SetResult("{no es json");

        var cut = await RenderEnAsync("/");

        Assert.DoesNotContain(true, Grupos(cut).Values);
        Assert.Contains("/usuarios", cut.Markup);
    }

    [Fact]
    public void El_encabezado_del_grupo_recibe_clics()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var css = Regex.Replace(
            File.ReadAllText(Path.Combine(dir.FullName, "PassPlat.Web", "wwwroot", "css", "app.css")),
            @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var declaraciones = Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}")
            .Where(regla => regla.Groups[1].Value.Split(',')
                .Any(selector => Regex.Replace(selector, @"\s+", " ").Trim() == Encabezado))
            .SelectMany(regla => regla.Groups[2].Value.Split(';'))
            .Select(declaracion => Regex.Replace(declaracion, @"\s+", string.Empty))
            .ToList();

        Assert.Contains("cursor:pointer", declaraciones);
        Assert.DoesNotContain("pointer-events:none", declaraciones);
    }
}
