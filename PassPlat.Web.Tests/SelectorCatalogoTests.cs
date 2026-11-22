using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;

namespace PassPlat.Web.Tests;

/// <summary>
/// S54 · K0.2 — selector de catalogo comun (D6).
/// S54.1 · K1 (D1-D4) — pie solo si sobra, estilo del pie, error derivado del estado
/// y strict real (el valor pasa a nulo al salir del campo con un texto que no es el suyo).
/// S59.1 · K2 (D2) — las escrituras del campo usan un ayudante que reintenta cuando el
/// nodo queda obsoleto (el componente se redibuja de forma asincrona).
/// </summary>
public class SelectorCatalogoTests : WebTestBase
{
    private static List<AppDto> Apps(int cantidad)
    {
        var lista = new List<AppDto>();
        for (var i = 0; i < cantidad; i++)
        {
            var (id, codigo, nombre) = Datos.Apps[i % Datos.Apps.Length];
            lista.Add(new AppDto { Id = id, Codigo = $"{codigo}_{i}", Nombre = $"{nombre} {i}", Activa = true, FecCrea = DateTime.UnixEpoch });
        }
        return lista;
    }

    /// <summary>
    /// S59.1 · D2 — acciona el input del selector y reintenta si el nodo quedo obsoleto.
    /// <c>SelectorCatalogo</c> se redibuja de forma asincrona (la busqueda con espera y el
    /// <c>StateHasChanged</c> posterior), asi que el nodo que devuelve <c>Find</c> puede quedar
    /// viejo y bUnit lanza <c>UnknownEventHandlerIdException</c>: se fuerza un redibujado del
    /// componente (<c>cut.Render()</c>) para que el nodo y su manejador vuelvan a coincidir y se
    /// vuelve a intentar, con un limite de intentos y un mensaje claro si se agotan.
    /// </summary>
    private static void AccionEnSelector<T>(IRenderedComponent<T> cut, Action<IElement> accion, string descripcion, int intentos = 10)
        where T : IComponent
    {
        Exception? ultimo = null;
        for (var i = 0; i < intentos; i++)
        {
            try
            {
                accion(cut.Find("input"));
                return;
            }
            catch (UnknownEventHandlerIdException ex)
            {
                ultimo = ex;
                // El DOM de bUnit puede ir un redibujado por detras del arbol del renderizador:
                // se fuerza un redibujado del componente para que el nodo y su manejador
                // vuelvan a coincidir, y se reintenta.
                try { cut.Render(); } catch (Exception) { /* se reintenta igual */ }
            }
        }
        throw new InvalidOperationException(
            $"No se pudo {descripcion} en el selector tras {intentos} intentos: el nodo queda obsoleto en cada redibujado.", ultimo);
    }

    private static void EscribirEnSelector<T>(IRenderedComponent<T> cut, string texto) where T : IComponent
        => AccionEnSelector(cut, i => i.Input(texto), $"escribir '{texto}'");

    private static void EnfocarEnSelector<T>(IRenderedComponent<T> cut) where T : IComponent
        => AccionEnSelector(cut, i => i.Focus(), "enfocar el campo");

    private static void SacarFocoEnSelector<T>(IRenderedComponent<T> cut) where T : IComponent
        => AccionEnSelector(cut, i => i.Blur(), "sacar el foco del campo");

    /// <summary>S59.1 · D2 — clic en el primer elemento de la lista, con el mismo reintento.</summary>
    private static void ClickEnPrimerItem<T>(IRenderedComponent<T> cut, int intentos = 10) where T : IComponent
    {
        Exception? ultimo = null;
        for (var i = 0; i < intentos; i++)
        {
            try
            {
                cut.FindAll(".mud-list-item").First().Click();
                return;
            }
            catch (UnknownEventHandlerIdException ex)
            {
                ultimo = ex;
                try { cut.WaitForState(() => cut.FindAll(".mud-list-item").Count > 0, TimeSpan.FromSeconds(5)); }
                catch (Exception) { /* se reintenta igualmente */ }
            }
        }
        throw new InvalidOperationException(
            $"No se pudo hacer clic en el primer elemento de la lista del selector tras {intentos} intentos.", ultimo);
    }

    private IRenderedComponent<TestHost> RenderAppSelector(IReadOnlyList<AppDto> items, Action<AppDto?>? onChanged = null, int maximo = 50)
        => Render<TestHost>(p => p.AddChildContent<SelectorCatalogo<AppDto>>(c => c
            .Add(x => x.Items, items)
            .Add(x => x.SearchTextSelector, (Func<AppDto, string?>)(a => $"{a.Nombre} {a.Codigo}"))
            .Add(x => x.ToStringFunc, (Func<AppDto, string>)(a => $"{a.Nombre} ({a.Codigo})"))
            .Add(x => x.Label, "App")
            .Add(x => x.MaximoVisible, maximo)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<AppDto?>(this, onChanged!))));

    private IRenderedComponent<TestHost> RenderTenantSelector(IReadOnlyList<TenantDto> items)
        => Render<TestHost>(p => p.AddChildContent<SelectorCatalogo<TenantDto>>(c => c
            .Add(x => x.Items, items)
            .Add(x => x.SearchTextSelector, (Func<TenantDto, string?>)(t => $"{t.Nombre} {t.Codigo}"))
            .Add(x => x.ToStringFunc, (Func<TenantDto, string>)(t => $"{t.Nombre} ({t.Codigo})"))
            .Add(x => x.Label, "Tenant")));

    private IRenderedComponent<SelectorAppHost> RenderEnlazado(IReadOnlyList<AppDto> items, AppDto? valorInicial = null, Action<AppDto?>? onChanged = null, int maximo = 50)
        => Render<SelectorAppHost>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ValorInicial, valorInicial)
            .Add(x => x.MaximoVisible, maximo)
            .Add(x => x.ValorCambiado, EventCallback.Factory.Create<AppDto?>(this, onChanged!)));

    [Fact]
    public void Con_12_apps_se_dibujan_12_y_sin_pie()
    {
        var cut = RenderAppSelector(Apps(12));

        EnfocarEnSelector(cut);

        cut.WaitForAssertion(() => Assert.Equal(12, cut.FindAll(".mud-list-item").Count));
        Assert.Empty(cut.FindAll("[data-testid=selector-catalogo-pie]"));
    }

    [Fact]
    public void Con_60_filas_se_dibujan_50_y_pie_de_total()
    {
        var cut = RenderAppSelector(Apps(60));

        EnfocarEnSelector(cut);

        cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(".mud-list-item").Count));
        var pie = cut.Find("[data-testid=selector-catalogo-pie]").TextContent;
        Assert.Contains("Mostrando 50 de 60", pie);
        Assert.Contains("escribe para filtrar", pie);
    }

    [Fact]
    public void El_estilo_del_pie_y_de_sin_resultados_esta_en_el_bloque_style()
    {
        var cut = RenderAppSelector(Apps(12));

        var estilos = cut.FindAll("style").Select(s => s.TextContent).ToList();
        Assert.Contains(estilos, e => e.Contains(".selector-catalogo-pie") && e.Contains("border-top"));
        Assert.Contains(estilos, e => e.Contains(".selector-catalogo-vacio"));
        Assert.Contains(estilos, e => e.Contains(".selector-catalogo-lista") && e.Contains("overscroll-behavior"));
    }

    [Fact]
    public void Inventa_encuentra_INVENTA_e_INVENTA_STORE()
    {
        var reales = new List<AppDto>
        {
            new() { Id = 1, Codigo = "PASSPLAT", Nombre = "AccessPlat", Activa = true, FecCrea = DateTime.UnixEpoch },
            new() { Id = 12, Codigo = "INVENTA", Nombre = "Inventa POS", Activa = true, FecCrea = DateTime.UnixEpoch },
            new() { Id = 13, Codigo = "INVENTA_STORE", Nombre = "Inventa Store", Activa = true, FecCrea = DateTime.UnixEpoch }
        };
        var cut = RenderAppSelector(reales);

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "inventa");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count));
        var textos = cut.FindAll(".mud-list-item").Select(e => e.TextContent).ToList();
        Assert.Contains(textos, t => t.Contains("Inventa POS (INVENTA)"));
        Assert.Contains(textos, t => t.Contains("Inventa Store (INVENTA_STORE)"));
    }

    [Fact]
    public void La_busqueda_ignora_mayusculas_y_tildes()
    {
        var tenants = new List<TenantDto>
        {
            new() { Id = 1, Codigo = "PLATFORM", Nombre = "Plataforma", Activo = true, EsSistema = true, FecCrea = DateTime.UnixEpoch },
            new() { Id = 2, Codigo = "ABARROTES", Nombre = "Abarrotes del Sur", Activo = true, FecCrea = DateTime.UnixEpoch }
        };
        var cut = RenderTenantSelector(tenants);

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "platafórma");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".mud-list-item")));
        Assert.Contains("Plataforma (PLATFORM)", cut.Find(".mud-list-item").TextContent);
    }

    [Fact]
    public void Un_texto_que_no_es_opcion_deja_el_valor_vacio_y_muestra_error()
    {
        AppDto? valor = null;
        var cut = RenderAppSelector(Apps(12), onChanged: v => valor = v);

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "no-existe-esta-opcion");

        cut.WaitForAssertion(() => Assert.Contains("no-existe-esta-opcion", cut.Find("[data-testid=selector-catalogo-error]").TextContent));
        Assert.Null(valor);
    }

    [Fact]
    public void El_error_desaparece_al_elegir_un_valor_valido()
    {
        var cut = RenderEnlazado(Apps(12));

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "no-existe-esta-opcion");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=selector-catalogo-error]")));

        // Se vuelve a pedir el input: el redibujado deja obsoleto el nodo anterior.
        EscribirEnSelector(cut, "Inventa POS");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));
        ClickEnPrimerItem(cut);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=selector-catalogo-error]")));
    }

    [Fact]
    public void Con_un_valor_elegido_se_escribe_otro_texto_y_al_salir_el_valor_pasa_a_nulo()
    {
        var apps = Apps(12);
        var elegido = apps[0];
        AppDto? valor = elegido;
        var cut = RenderEnlazado(apps, valorInicial: elegido, onChanged: v => valor = v);

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "zzzz");
        cut.WaitForAssertion(() => Assert.Contains("zzzz", cut.Find("input").GetAttribute("value") ?? ""));
        Assert.Equal(elegido, valor);

        SacarFocoEnSelector(cut);

        cut.WaitForAssertion(() => Assert.Null(valor));
        cut.WaitForAssertion(() => Assert.Contains("zzzz", cut.Find("[data-testid=selector-catalogo-error]").TextContent));
    }

    [Fact]
    public void Al_vaciar_el_texto_el_valor_queda_nulo_y_sin_error()
    {
        var apps = Apps(12);
        var elegido = apps[0];
        AppDto? valor = elegido;
        var cut = RenderEnlazado(apps, valorInicial: elegido, onChanged: v => valor = v);

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "");
        SacarFocoEnSelector(cut);

        cut.WaitForAssertion(() => Assert.Null(valor));
        Assert.Empty(cut.FindAll("[data-testid=selector-catalogo-error]"));
    }

    [Fact]
    public void Sin_coincidencias_muestra_el_mensaje_sin_resultados()
    {
        var cut = RenderAppSelector(Apps(12));

        EnfocarEnSelector(cut);
        EscribirEnSelector(cut, "no-existe-esta-opcion");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=selector-catalogo-vacio]")));
        Assert.Contains("Sin resultados", cut.Find("[data-testid=selector-catalogo-vacio]").TextContent);
    }

    [Fact]
    public void La_comparacion_ignora_tildes_y_mayusculas()
    {
        Assert.True(SelectorCatalogo<AppDto>.Coincide("Inventa POS INVENTA", "inventa"));
        Assert.True(SelectorCatalogo<AppDto>.Coincide("Plataforma", "platafórma"));
        Assert.False(SelectorCatalogo<AppDto>.Coincide("Inventa POS", "vestuario"));
        Assert.True(SelectorCatalogo<AppDto>.Coincide("cualquiera", ""));
    }

    /// <summary>
    /// S54.2 · K3 (D3) — al salir de inmediato con un texto sin coincidencias, el valor pasa
    /// a nulo y se ve el error sin esperar a la busqueda de MudBlazor.
    /// </summary>
    [Fact]
    public void Al_salir_de_inmediato_con_un_texto_sin_coincidencias_se_ve_el_error()
    {
        var apps = Apps(12);
        var elegido = apps[0];
        AppDto? valor = elegido;
        var cut = RenderEnlazado(apps, valorInicial: elegido, onChanged: v => valor = v);

        EnfocarEnSelector(cut);
        // La busqueda inicial deja el conteo en 12 (el texto del valor no esta en el catalogo).
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")));

        // Sin esperar: se escribe y se sale del campo de inmediato.
        EscribirEnSelector(cut, "zzzz");
        SacarFocoEnSelector(cut);

        Assert.Null(valor);
        Assert.NotEmpty(cut.FindAll("[data-testid=selector-catalogo-error]"));
        Assert.Contains("no coincide con ninguna opción", cut.Find("[data-testid=selector-catalogo-error]").TextContent);
    }
}
