using System.Reflection;
using CBP.Blazor.Mud;
using Microsoft.AspNetCore.Components;
using PassPlat.Web.Layout;
using PassPlat.Web.Services;

namespace PassPlat.Web.Tests;

/// <summary>
/// S83 · D4 — catálogo de navegación: título de la barra (nunca la ruta cruda), buscador sin tildes
/// y grupos del menú a partir del árbol de <c>api/modulos/menu</c>.
/// </summary>
public class S83CatalogoNavegacionTests
{
    private static readonly CatalogoNavegacion Catalogo = new(S83MenuDePrueba.Arbol());

    [Fact]
    public void Titulo_de_ruta_del_catalogo_usa_nombre_y_descripcion()
    {
        var (titulo, subtitulo, icono) = Catalogo.TituloDe("https://localhost:7275/auditoria");

        Assert.Equal("Eventos de auditoría", titulo);
        Assert.Equal("Eventos de seguridad del sistema", subtitulo);
        Assert.Equal(IconosMud.PorNombre("History"), icono);
        Assert.Equal("Eventos de auditoría", Catalogo.TituloDe("https://localhost:7275/AUDITORIA/?x=1").Titulo);
        Assert.Equal("Panel principal", Catalogo.TituloDe("https://localhost:7275/").Titulo);
    }

    [Theory]
    [InlineData("https://x/tenant/5", "Ficha de tenant")]
    [InlineData("https://x/usuario/1", "Ficha de usuario")]
    [InlineData("https://x/federacion/iden-ext/3/9", "Identidad externa")]
    [InlineData("https://x/federacion/confproviden/1/2", "Configuración de proveedor")]
    [InlineData("https://x/dispositivos/7", "Ficha de dispositivo")]
    [InlineData("https://x/dispositivos-confiables", "Dispositivos confiables")]
    [InlineData("https://x/admin/dashboard-enterprise", "Panel de administración")]
    [InlineData("https://x/admin/iam-dashboard", "Panel de administración")]
    [InlineData("https://x/rol/4", "Ficha de rol")]
    public void Titulo_de_ficha_y_alias_nunca_muestra_la_ruta_cruda(string uri, string esperado)
    {
        var (titulo, _, _) = Catalogo.TituloDe(uri);

        Assert.Equal(esperado, titulo);
        Assert.DoesNotContain("/", titulo);
    }

    [Theory]
    [InlineData("https://x/a--b/")]
    [InlineData("https://x//")]
    [InlineData("https://x/sin-modulo")]
    [InlineData("no es una uri")]
    [InlineData("")]
    [InlineData(null)]
    public void Ruta_desconocida_con_segmento_vacio_no_lanza(string? uri)
    {
        var (titulo, subtitulo, _) = Catalogo.TituloDe(uri);

        Assert.True(titulo == "PassPlat" || titulo == "Panel principal", titulo);
        Assert.Equal("PassPlat", CatalogoNavegacion.Vacio.TituloDe(uri).Titulo);
        Assert.NotNull(subtitulo);
    }

    [Fact]
    public void Buscar_ignora_tildes_y_corta_en_ocho()
    {
        Assert.Equal("Eventos de auditoría", Assert.Single(Catalogo.Buscar("auditoria")).Nombre);
        Assert.Equal("Eventos de auditoría", Assert.Single(Catalogo.Buscar("AUDITORÍA")).Nombre);
        Assert.Contains(Catalogo.Buscar("ses"), m => m.Nombre == "Sesiones");
        Assert.Contains(Catalogo.Buscar("confiables"), m => m.Ruta == "/disp-confiables");
        Assert.Empty(Catalogo.Buscar("a"));
        Assert.Empty(Catalogo.Buscar("produc"));

        var muchos = new CatalogoNavegacion(
            Enumerable.Range(1, 12).Select(i => S83MenuDePrueba.Nodo($"R{i}", $"Informe {i:00}", $"/informe-{i}")).ToList());
        Assert.Equal(8, muchos.Buscar("informe").Count);

        var orden = new CatalogoNavegacion(
        [
            S83MenuDePrueba.Nodo("A", "Historial de sesiones", "/a"),
            S83MenuDePrueba.Nodo("B", "Sesiones", "/b")
        ]);
        Assert.Equal(["Sesiones", "Historial de sesiones"], orden.Buscar("sesion").Select(m => m.Nombre));
    }

    [Fact]
    public void Grupo_sin_enlaces_de_menu_no_existe()
    {
        var grupos = Catalogo.Grupos;

        Assert.Equal(["PASSPLAT.INICIO", "IAM", "AUDITORIA", "EMAIL"], grupos.Select(g => g.Raiz.Codigo));
        Assert.Empty(grupos[0].Enlaces);
        Assert.Equal(["/usuarios", "/admin/panel", "/admin/roles"], grupos[1].Enlaces.Select(e => e.Ruta));
        Assert.DoesNotContain(grupos.SelectMany(g => g.Enlaces), e => e.Ruta == "/sesiones");
    }

    [Fact]
    public void Toda_pagina_con_MainLayout_tiene_titulo()
    {
        var catalogo = new CatalogoNavegacion(
            S83MenuDePrueba.RutasDelSeed.Select((r, i) => S83MenuDePrueba.Nodo($"M{i}", $"Módulo {i}", r)).ToList());
        var rutas = typeof(MainLayout).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<LayoutAttribute>()?.LayoutType != typeof(PassPlat.Web.Pages.LoginLayout))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(r => System.Text.RegularExpressions.Regex.Replace(r.Template, @"\{[^}]+\}", "1"))
            .Distinct()
            .ToList();

        Assert.NotEmpty(rutas);
        var sinTitulo = rutas.Where(r =>
        {
            var titulo = catalogo.TituloDe("https://localhost:7275" + r).Titulo;
            return titulo == "PassPlat" || titulo.Contains('/');
        }).ToList();
        Assert.Empty(sinTitulo);
    }
}
