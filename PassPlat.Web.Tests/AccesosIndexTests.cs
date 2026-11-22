using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using PassPlat.Web.Pages.Accesos;
using PassPlat.Web.Tests.Infra;
using AccesosIndex = PassPlat.Web.Pages.Accesos.Index;

namespace PassPlat.Web.Tests;

/// <summary>S54 · K3 — pagina de Accesos (D2): filtro por usuario y refresco tras asignar.</summary>
public class AccesosIndexTests : WebTestBase
{
    private readonly Mock<IDialogService> _servicioDialogos = new();

    public AccesosIndexTests()
    {
        Services.AddSingleton(_servicioDialogos.Object);
        CargarCatalogos();
        Handler.Responder("/api/accesos/usuario/1", Datos.AccesosJson((1, 1, 1, "Administrador")));
        Handler.Responder("/api/accesos/usuario/2", Datos.AccesosJson((2, 12, 1, "Administrador")));
    }

    private async Task<IRenderedComponent<AccesosIndex>> RenderPaginaAsync()
    {
        await AutenticarAsync();
        return Render<AccesosIndex>();
    }

    [Fact]
    public async Task La_pagina_tiene_filtro_por_usuario_y_lista_los_accesos_del_usuario_conectado()
    {
        var cut = await RenderPaginaAsync();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=accesos-filtro-usuario]")));
        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));
        Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/accesos/usuario/1"));
    }

    [Fact]
    public async Task Tras_asignar_la_lista_muestra_al_usuario_asignado()
    {
        var referencia = new Mock<IDialogReference>();
        referencia.Setup(r => r.Result).Returns(Task.FromResult<DialogResult?>(DialogResult.Ok(2)));
        _servicioDialogos
            .Setup(d => d.ShowAsync<AccesoDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(referencia.Object);

        var cut = await RenderPaginaAsync();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=accesos-filtro-usuario]")));

        cut.FindAll("button").First(b => b.TextContent.Contains("Asignar Acceso")).Click();

        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.Contains("GET /api/accesos/usuario/2")));
    }

    /// <summary>S55 · K4 (D5/U8) — el buscador filtra de verdad la tabla.</summary>
    [Fact]
    public async Task El_buscador_filtra_la_tabla()
    {
        var cut = await RenderPaginaAsync();
        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));

        var input = cut.FindAll("input").First(i =>
        {
            var id = i.GetAttribute("id");
            var label = id is null ? null : cut.FindAll("label").FirstOrDefault(l => l.GetAttribute("for") == id);
            return label?.TextContent.Contains("Buscar", StringComparison.OrdinalIgnoreCase) == true;
        });
        input.Input("Inventa");
        cut.WaitForAssertion(() => Assert.Contains("Inventa POS", cut.Markup));

        // Se vuelve a pedir el input: el filtro redibuja la tabla y el nodo anterior queda obsoleto.
        var input2 = cut.FindAll("input").First(i =>
        {
            var id = i.GetAttribute("id");
            var label = id is null ? null : cut.FindAll("label").FirstOrDefault(l => l.GetAttribute("for") == id);
            return label?.TextContent.Contains("Buscar", StringComparison.OrdinalIgnoreCase) == true;
        });
        input2.Input("no-existe-este-acceso");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Inventa POS", cut.Markup));
    }
}
