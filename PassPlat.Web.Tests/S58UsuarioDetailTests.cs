using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Tests.Infra;
using UsuarioDetail = PassPlat.Web.Pages.Usuarios.UsuarioDetail;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58 · K3 (D3/D4) — ficha de usuario: la miga muestra el nombre del usuario (nunca
/// el texto literal del codigo), el titulo de la barra no muestra la ruta cruda y el
/// usuario se pide una sola vez.
/// </summary>
public class S58UsuarioDetailTests : WebTestBase
{
    private const string Usuario = """
    {
      "id": 1, "idTenant": 1, "nomUsuario": "sistema", "email": "sistema@passplat.app",
      "nombre": "Usuario", "apellido": "Sistema", "eliminado": false, "estadoNombre": "Activo",
      "tenantNombre": "Plataforma", "emailVerificado": true, "reqCambioPwd": false
    }
    """;

    public S58UsuarioDetailTests()
    {
        Handler.Responder("/api/usuarios/1", Usuario);
        Handler.Responder("/api/usuarios/password-policy", "{}");
    }

    private async Task<IRenderedComponent<UsuarioDetail>> RenderFichaAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "USUARIOS_VER");
        return Render<UsuarioDetail>(p => p.Add(x => x.Id, 1));
    }

    [Fact]
    public async Task La_miga_muestra_el_nombre_del_usuario()
    {
        var cut = await RenderFichaAsync();

        cut.WaitForAssertion(() => Assert.Contains("sistema", cut.Markup));
        Assert.DoesNotContain("@_usuario.NomUsuario", cut.Markup);
    }

    [Fact]
    public async Task El_usuario_se_pide_una_sola_vez()
    {
        await RenderFichaAsync();

        Assert.Single(Handler.Peticiones, p => p.Contains("GET /api/usuarios/1"));
    }

    [Fact]
    public void La_pagina_no_deja_el_texto_literal_en_la_miga()
    {
        var raiz = RaizDelRepo();
        var pagina = File.ReadAllText(Path.Combine(raiz, "PassPlat.Web", "Pages", "Usuarios", "UsuarioDetail.razor"));

        Assert.DoesNotContain("\"@_usuario.NomUsuario\"", pagina);
        Assert.Contains("_usuario?.NomUsuario ?? \"Usuario\"", pagina);
    }

    [Fact]
    public void El_titulo_de_la_barra_no_muestra_la_ruta_cruda()
    {
        // S83 · K3 — el titulo sale del catalogo de navegacion: misma intencion (S58 · D3), probada por comportamiento.
        Assert.Equal("Ficha de usuario", PassPlat.Web.Services.CatalogoNavegacion.Vacio.TituloDe("https://x/usuario/1").Titulo);
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }
}
