using System.Reflection;
using System.Text.RegularExpressions;
using Bunit;
using ConfProvIdenIndex = PassPlat.Web.Pages.Federacion.ConfProvIden.Index;
using Dashboard = PassPlat.Web.Pages.Dashboard;
using DominiosTenantIndex = PassPlat.Web.Pages.DominiosTenant.Index;
using PanelAdmin = PassPlat.Web.Pages.Admin.DashboardEnterprise;

namespace PassPlat.Web.Tests;

/// <summary>
/// S71 · K3 — la Web no inventa tenant ni usuario de reserva: en modo Plataforma las páginas
/// por tenant no piden <c>…/tenant/1</c> y muestran el aviso; con tenant piden el suyo.
/// </summary>
public class S71SinTenantDeReservaTests : WebTestBase
{
    private const string ConfProvIdenPagina = """
    {
      "items": [],
      "totalCount": 7,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
    """;

    private const string Aviso = "[data-testid=aviso-sin-tenant]";

    private static readonly Regex Reserva = new(@"(IdTenant|IdUsuario)\s*\?\?\s*1\b|tenant/1""", RegexOptions.Compiled);

    private Task SesionPlataformaAsync(params string[] permisos) =>
        Auth.RestoreSessionAsync(Token(0, permisos), null, 1, 0, null, "sistema");

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static T Campo<T>(object componente, string nombre) =>
        (T)componente.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(componente)!;

    private bool AlgunaPeticionContiene(string texto) =>
        Handler.Peticiones.Any(p => p.Contains(texto, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task ConfProvIden_no_pide_tenant_fijo_y_total_sale_de_la_pagina()
    {
        Handler.Responder("/api/confproviden/page", ConfProvIdenPagina);
        await AutenticarAsync();

        var cut = Render<ConfProvIdenIndex>();

        cut.WaitForAssertion(() => Assert.Contains(">7</", cut.Markup));
        Assert.True(AlgunaPeticionContiene("confproviden/page"));
        Assert.False(AlgunaPeticionContiene("confproviden/tenant/"));
    }

    [Fact]
    public async Task DominiosTenant_en_plataforma_muestra_aviso_y_no_pide_api()
    {
        await SesionPlataformaAsync("TENANTS_VER");

        var cut = Render<DominiosTenantIndex>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(Aviso)));
        Assert.False(AlgunaPeticionContiene("DominiosTenant/tenant/"));
    }

    [Fact]
    public async Task DominiosTenant_con_tenant_pide_su_tenant()
    {
        await AutenticarAsync(idTenant: 3, permisos: "TENANTS_VER");

        var cut = Render<DominiosTenantIndex>();

        cut.WaitForAssertion(() => Assert.True(AlgunaPeticionContiene("DominiosTenant/tenant/3")));
        Assert.Empty(cut.FindAll(Aviso));
    }

    [Fact]
    public async Task Panel_en_plataforma_pide_resumen_global()
    {
        await SesionPlataformaAsync("USUARIOS_VER");

        var cut = Render<PanelAdmin>();

        cut.WaitForAssertion(() => Assert.False(Campo<bool>(cut.Instance, "_loading")));
        Assert.True(AlgunaPeticionContiene("api/dashboard-enterprise/ejecutivo"));
        Assert.False(AlgunaPeticionContiene("/tenant/"));
    }

    [Fact]
    public async Task Dashboard_en_plataforma_no_pide_federacion_de_tenant()
    {
        await SesionPlataformaAsync("AUDITORIA_VER");

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.True(Campo<bool>(cut.Instance, "_cargado")));
        Assert.NotNull(cut.Find(Aviso));
        Assert.False(AlgunaPeticionContiene("federacion/estadisticas/"));
        Assert.False(AlgunaPeticionContiene("AuditoriaPwd/tenant/"));
    }

    [Fact]
    public void Web_no_usa_tenant_ni_usuario_de_reserva()
    {
        var paginas = Path.Combine(RaizDelRepo(), "PassPlat.Web", "Pages");
        var pendientes = new List<string>();

        foreach (var archivo in Directory.EnumerateFiles(paginas, "*.razor", SearchOption.AllDirectories))
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                if (Reserva.IsMatch(lineas[i]))
                    pendientes.Add($"{Path.GetRelativePath(paginas, archivo).Replace('\\', '/')}:{i + 1}");
            }
        }

        Assert.Empty(pendientes);
    }
}
