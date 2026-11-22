using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PassPlat.Web.Shared;
using GrupoDetail = PassPlat.Web.Pages.Grupos.GrupoDetail;
using GruposIndex = PassPlat.Web.Pages.Grupos.Index;
using RolDetail = PassPlat.Web.Pages.Roles.RolDetail;
using RolesIndex = PassPlat.Web.Pages.Roles.Index;

namespace PassPlat.Web.Tests;

/// <summary>
/// S69.2 · K3 — Roles y Grupos sin inspector: un clic en la fila navega a
/// <c>/rol/{id}</c> o <c>/grupo/{id}</c>, y las páginas de detalle muestran el
/// contenido que antes tenía el panel lateral.
/// </summary>
public class S692RolesGruposDetalleTests : WebTestBase
{
    private const string Roles = """
    [
      { "id": 3, "idTenant": null, "codigo": "AUDITOR", "nombre": "Auditor Interno", "descripcion": "Solo lectura", "activo": true, "fecCrea": "2026-01-01T00:00:00Z", "tenantNombre": null }
    ]
    """;

    private const string Grupos = """
    [
      { "id": 5, "idTenant": 1, "codigo": "SOPORTE", "nombre": "Equipo Soporte", "descripcion": "Mesa de ayuda", "activo": true, "fecCrea": "2026-01-01T00:00:00Z", "tenantNombre": "Plataforma" }
    ]
    """;

    private const string GruposStats = """
    { "5": { "miembros": 2, "usuarios": 2 } }
    """;

    public S692RolesGruposDetalleTests()
    {
        Handler.Responder("/api/roles", Roles);
        Handler.Responder("/api/roles/para-tenant/1", Roles);
        Handler.Responder("/api/grupos", Grupos);
        Handler.Responder("/api/grupos/stats", GruposStats);
    }

    private NavigationManager Navegacion => Services.GetRequiredService<NavigationManager>();

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public async Task Roles_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync(permisos: "ROLES_DETALLE_VER");
        var cut = Render<RolesIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/rol/3", Navegacion.Uri));
    }

    [Fact]
    public async Task Grupos_clic_en_fila_navega_al_detalle()
    {
        await AutenticarAsync(permisos: "GRUPOS_VER");
        var cut = Render<GruposIndex>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tbody tr.mud-table-row")));

        cut.FindAll("tbody tr.mud-table-row")[0].Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/grupo/5", Navegacion.Uri));
    }

    [Fact]
    public async Task RolDetail_muestra_nombre_y_cinco_pestanas()
    {
        await AutenticarAsync(permisos: "ROLES_DETALLE_VER");
        var cut = Render<RolDetail>(p => p.Add(x => x.Id, 3));

        cut.WaitForAssertion(() => Assert.Contains("Auditor Interno", cut.Markup));
        foreach (var pestana in new[] { "Info", "Permisos", "Herencia", "Usuarios", "Apps" })
            Assert.Contains(pestana, cut.Markup);
    }

    [Fact]
    public async Task RolDetail_sin_permiso_muestra_SinPermiso()
    {
        await AutenticarAsync(permisos: "USUARIOS_VER");
        var cut = Render<RolDetail>(p => p.Add(x => x.Id, 3));

        Assert.NotEmpty(cut.FindComponents<SinPermiso>());
        Assert.DoesNotContain(Handler.Peticiones, p => p.Contains("/api/roles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GrupoDetail_muestra_nombre_y_pestanas()
    {
        await AutenticarAsync(permisos: "GRUPOS_VER");
        var cut = Render<GrupoDetail>(p => p.Add(x => x.Id, 5));

        cut.WaitForAssertion(() => Assert.Contains("Equipo Soporte", cut.Markup));
        Assert.Contains("Miembros", cut.Markup);
    }

    [Fact]
    public async Task GrupoDetail_id_inexistente_muestra_no_encontrado()
    {
        await AutenticarAsync(permisos: "GRUPOS_VER");
        var cut = Render<GrupoDetail>(p => p.Add(x => x.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Grupo no encontrado", cut.Markup));
    }

    [Fact]
    public void Roles_y_Grupos_sin_inspector()
    {
        var raiz = RaizDelRepo();
        var archivos = new[]
        {
            Path.Combine("PassPlat.Web", "Pages", "Roles", "Index.razor"),
            Path.Combine("PassPlat.Web", "Pages", "Grupos", "Index.razor")
        };
        var pendientes = new List<string>();

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(Path.Combine(raiz, archivo));
            foreach (var patron in new[] { "class=\"inspector\"", "insp-", "_rolSeleccionado", "_grupoSeleccionado", "SelectedItemChanged" })
            {
                if (texto.Contains(patron, StringComparison.Ordinal))
                    pendientes.Add($"{archivo.Replace('\\', '/')}: {patron}");
            }
        }

        Assert.Empty(pendientes);
    }
}
