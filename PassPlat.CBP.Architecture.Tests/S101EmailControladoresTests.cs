using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S101 · D3 — el catálogo global de Email (providers, cuentas, cuentas por app) solo lo escribe
/// el sistema (<c>SystemOnly</c>); las cuentas por tenant se acotan al tenant del token.
/// </summary>
public sealed class S101EmailControladoresTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string controlador) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", controlador));

    /// <summary>Verbo de cada acción de escritura y la política de la línea siguiente (null si no hay).</summary>
    private static List<(string Verbo, string? Politica)> AccionesDeEscritura(string fuente)
    {
        var lineas = fuente.Split('\n');
        var acciones = new List<(string, string?)>();
        for (var i = 0; i < lineas.Length; i++)
        {
            var verbo = Regex.Match(lineas[i], @"^\s*\[Http(Post|Put|Delete)\b");
            if (!verbo.Success) continue;
            var politica = i + 1 < lineas.Length ? Regex.Match(lineas[i + 1], @"\[Authorize\(Policy = ""(\w+)""\)\]") : Match.Empty;
            acciones.Add((verbo.Groups[1].Value, politica.Success ? politica.Groups[1].Value : null));
        }
        return acciones;
    }

    [Fact]
    public void Las_escrituras_del_catalogo_de_email_exigen_SystemOnly()
    {
        Assert.Equal(
            new List<(string, string?)> { ("Post", "SystemOnly"), ("Put", "SystemOnly") },
            AccionesDeEscritura(Leer("EmailProvidersController.cs")));
        Assert.Equal(
            new List<(string, string?)> { ("Post", "SystemOnly"), ("Put", "SystemOnly") },
            AccionesDeEscritura(Leer("EmailAccountsController.cs")));
        Assert.Equal(
            new List<(string, string?)> { ("Post", "SystemOnly"), ("Delete", "SystemOnly") },
            AccionesDeEscritura(Leer("AppEmailAccountsController.cs")));

        // la lectura sigue con su política _VER de clase
        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""EMAIL_PROVIDERS_VER""\)\]\s*public class EmailProvidersController : BaseApiController\b"),
            Leer("EmailProvidersController.cs"));
    }

    [Fact]
    public void TenantEmailAccounts_acota_lectura_alta_y_baja_por_tenant()
    {
        var fuente = Leer("TenantEmailAccountsController.cs");

        Assert.Contains("ITenantContext", fuente);
        Assert.Contains("User.AlcanceTenant(_tenantContext)", fuente);
        Assert.Matches(new Regex(@"GetByTenant\(int idTenant[^{]*\{\s*if \(FueraDeAlcance\(idTenant\)\) return NotFound\(\);"), fuente);
        Assert.Matches(new Regex(@"Create\(\[FromBody\] CrearTenantEmailAccountDto dto[^{]*\{\s*if \(FueraDeAlcance\(dto\.IdTenant\)\) return NotFound\(\);"), fuente);
        Assert.Matches(new Regex(@"GetById\(int id[^{]*\{[^}]*FueraDeAlcance\(result\.Value\.IdTenant\)\) return NotFound\(\);"), fuente);
        Assert.Contains("_service.EliminarAsync(id, AlcanceTenant, ct)", fuente);
    }
}
