using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Shared;
using PassPlat.Web.Tests.Infra;
using RolesPermisos = PassPlat.Web.Pages.RolesPermisos.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S58.1 · K3 (D3) y K5 (D5) — los dialogos de formulario usan el helper de tamanos
/// y la página de permisos directos (S102) elige un usuario y lista sus permisos.
/// </summary>
public class S581DialogosYRolesTests : WebTestBase
{
    public S581DialogosYRolesTests() => Handler.Responder("/api/usuarios", Datos.UsuariosJson());

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public void Los_dialogos_de_formulario_usan_el_helper_de_tamanos()
    {
        var raiz = RaizDelRepo();
        var carpeta = Path.Combine(raiz, "PassPlat.Web");
        var sinHelper = new List<string>();

        foreach (var archivo in Directory.GetFiles(carpeta, "*.razor", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                if (!lineas[i].Contains("ShowAsync<")) continue;
                if (lineas[i].Contains("ShowAsync<ConfirmDialog>")) continue;

                var llamada = lineas[i];
                var j = i;
                while (!llamada.Contains(");") && j + 1 < lineas.Length && j - i < 8)
                {
                    j++;
                    llamada += " " + lineas[j].Trim();
                }

                if (!llamada.Contains("OpcionesDialogo."))
                    sinHelper.Add($"{Path.GetFileName(archivo)}:{i + 1}");
            }
        }

        Assert.True(sinHelper.Count == 0,
            $"ShowAsync de dialogos de formulario sin el helper de tamanos: {string.Join(", ", sinHelper)}");
    }

    [Fact]
    public void El_helper_ofrece_compacto_estandar_y_amplio()
    {
        Assert.Equal(MudBlazor.MaxWidth.Small, OpcionesDialogo.MaxWidthDe(TamanoDialogo.Compacto));
        Assert.Equal(MudBlazor.MaxWidth.Medium, OpcionesDialogo.MaxWidthDe(TamanoDialogo.Estandar));
        Assert.Equal(MudBlazor.MaxWidth.Large, OpcionesDialogo.MaxWidthDe(TamanoDialogo.Amplio));
        Assert.Equal(TamanoDialogo.Compacto, OpcionesDialogo.SegunCampos(4));
        Assert.Equal(TamanoDialogo.Estandar, OpcionesDialogo.SegunCampos(5));
        Assert.Equal(TamanoDialogo.Amplio, OpcionesDialogo.SegunCampos(13));
        var opciones = OpcionesDialogo.Dialogo(TamanoDialogo.Estandar);
        Assert.Equal(MudBlazor.MaxWidth.Medium, opciones.MaxWidth);
        Assert.True(opciones.FullWidth);    }

    // S102: la página maestro-detalle se retiró; ahora prueba la página corta de permisos directos.
    private async Task<IRenderedComponent<TestHost>> RenderPaginaAsync()
    {
        await AutenticarAsync(1, 1, "sistema", "PERMISOS_VER", "PERMISOS_EDITAR");
        var cut = Render<TestHost>(p => p.AddChildContent<RolesPermisos>());
        cut.WaitForAssertion(() => Assert.Contains(Handler.Peticiones, p => p.StartsWith("GET /api/usuarios")), TimeSpan.FromSeconds(10));
        return cut;
    }

    [Fact]
    public async Task La_pagina_muestra_las_migas_y_el_selector_de_usuario()
    {
        var cut = await RenderPaginaAsync();

        Assert.Equal("Permisos directos", cut.Find("h1.page-title").TextContent.Trim());
        Assert.NotEmpty(cut.FindComponents<MigasDePan>());
        Assert.Contains(cut.FindAll("label"), l => l.TextContent.Trim().StartsWith("Usuario", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sin_usuario_elegido_no_pide_permisos_directos()
    {
        var cut = await RenderPaginaAsync();

        Assert.Contains("Elija un usuario", cut.Markup);
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("UsuariosPermisos"));
    }

    [Fact]
    public async Task Al_elegir_un_usuario_se_listan_sus_permisos()
    {
        // S103: la página usa el editor unificado (contextos + editor) en lugar de la lista de directos
        Handler.Responder("/api/UsuariosPermisos/contextos/2", """
        [{ "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "appCodigo": "PASSPLAT", "roles": [{ "idRol": 3, "nombre": "Admin" }] }]
        """);
        Handler.Responder("/api/UsuariosPermisos/editor/2", """
        { "idTenant": 1, "tenantNombre": "PLATFORM", "idApp": 1, "appNombre": "AccessPlat", "esSistema": false,
          "roles": [{ "idRol": 3, "nombre": "Admin" }],
          "permisos": [{ "idPermiso": 10, "codigo": "USUARIOS_VER", "nombre": "Ver usuarios", "idModulo": 7, "moduloNombre": "Usuarios", "orden": 1,
                         "porRol": true, "rolOrigen": "Admin", "directoVigente": false, "tiene": true }] }
        """);
        var cut = await RenderPaginaAsync();

        var label = cut.FindAll("label").First(l => l.TextContent.Trim().StartsWith("Usuario", StringComparison.Ordinal));
        var input = cut.Find($"input#{label.GetAttribute("for")}");
        input.Focus();
        input.Input("admin_plat");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(10));
        cut.FindAll(".mud-list-item").First(e => e.TextContent.Contains("admin_plataforma")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Ver usuarios", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Contains("Tiene 1 de 1", cut.Markup);
        Assert.Single(Handler.Peticiones, p => p.StartsWith("GET /api/UsuariosPermisos/contextos/2"));
    }
}
