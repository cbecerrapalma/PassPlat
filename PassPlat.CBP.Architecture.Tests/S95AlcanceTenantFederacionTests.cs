using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S95 · P47 — AuditoriaPwd, ConfProvIden e IdenExt leen con el alcance de tenant de la sesión:
/// un no-sistema solo ve su tenant (una ruta con otro tenant responde 404) y los filtros de las
/// listas los aplica la consulta.
/// </summary>
public sealed class S95AlcanceTenantFederacionTests
{
    private const string Alcance =
        "private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);";

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string controlador) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", controlador));

    [Theory]
    [InlineData("AuditoriaPwdController.cs", 0)]
    [InlineData("ConfProvIdenController.cs", 2)]
    [InlineData("IdenExtController.cs", 1)]
    public void S95_controlador_define_el_alcance_y_responde_404_fuera_de_el(string archivo, int rutasConTenant)
    {
        var fuente = Leer(archivo);

        Assert.Contains(Alcance, fuente);
        Assert.Equal(rutasConTenant, Regex.Matches(fuente, @"if \(FueraDeAlcance\(idTenant\)\) return NotFound\(\);").Count);
    }

    [Fact]
    public void S95_auditoria_lee_siempre_con_filtro_de_tenant()
    {
        var fuente = Leer("AuditoriaPwdController.cs");

        Assert.DoesNotContain("_service.GetAllAsync(", fuente);
        Assert.DoesNotContain("_service.GetPagedAsync(", fuente);
        Assert.DoesNotContain("_service.ObtenerPorUsuarioAsync(", fuente);
        Assert.DoesNotContain("_service.ObtenerEventosAltoRiesgoAsync(", fuente);
        Assert.Equal(3, Regex.Matches(fuente, @"_service\.BuscarAsync\(new FiltroAuditoriaPwd\(IdTenant: ").Count);
    }

    [Fact]
    public void S95_listas_de_federacion_fijan_el_tenant_de_la_sesion()
    {
        Assert.Contains("if (AlcanceTenant is int propio) request.IdTenant = propio;", Leer("IdenExtController.cs"));
        Assert.Contains("_service.BuscarPaginadoAsync(AlcanceTenant ?? idTenant, search,", Leer("ConfProvIdenController.cs"));
    }
}
