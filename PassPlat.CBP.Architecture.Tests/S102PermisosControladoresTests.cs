using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S102 · D5-D6 — permisos directos, herencia de roles y políticas por rol: leer con _VER,
/// escribir con _EDITAR, sobre <c>BaseApiController</c>, y un no-sistema acotado a su tenant.
/// </summary>
public sealed class S102PermisosControladoresTests
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
    public void UsuariosPermisos_exige_PERMISOS_VER_y_EDITAR_sobre_BaseApiController()
    {
        var fuente = Leer("UsuariosPermisosController.cs");

        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""PERMISOS_VER""\)\]\s*public class UsuariosPermisosController : BaseApiController\b"), fuente);
        Assert.Equal(
            new List<(string, string?)> { ("Post", "PERMISOS_EDITAR"), ("Delete", "PERMISOS_EDITAR") },
            AccionesDeEscritura(fuente));
        Assert.DoesNotContain("BadRequest(new { error", fuente);
    }

    [Fact]
    public void UsuariosPermisos_acota_por_tenant()
    {
        var fuente = Leer("UsuariosPermisosController.cs");

        Assert.Contains("ITenantContext", fuente);
        Assert.Contains("User.AlcanceTenant(_tenantContext)", fuente);
        Assert.Contains("_service.ObtenerPorUsuarioAsync(idUsuario, AlcanceTenant, ct)", fuente);
        Assert.Matches(new Regex(@"Aplicar\(\[FromBody\] AplicarPermisosUsuarioDto dto[^{]*\{\s*if \(FueraDeAlcance\(dto\.IdTenant\)\) return NotFound\(\);"), fuente);
        Assert.Matches(new Regex(@"GetEditor\(int idUsuario[^{]*\{\s*if \(FueraDeAlcance\(idTenant\)\) return NotFound\(\);"), fuente);
        Assert.Contains("_service.ObtenerContextosAsync(idUsuario, AlcanceTenant, ct)", fuente);
        Assert.Contains("_service.RevocarPermisoAsync(id, AlcanceTenant, User.IdUsuario() ?? 0, ct)", fuente);
    }

    [Fact]
    public void RolesHerencia_exige_ROLES_VER_y_EDITAR_y_acota_por_tenant()
    {
        var fuente = Leer("RolesHerenciaController.cs");

        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""ROLES_VER""\)\]\s*public class RolesHerenciaController : BaseApiController\b"), fuente);
        Assert.Equal(
            new List<(string, string?)> { ("Post", "ROLES_EDITAR"), ("Delete", "ROLES_EDITAR") },
            AccionesDeEscritura(fuente));
        Assert.Matches(new Regex(@"GetByTenant\(int idTenant[^{]*\{\s*if \(FueraDeAlcance\(idTenant\)\) return NotFound\(\);"), fuente);
        Assert.Matches(new Regex(@"Create\(\[FromBody\] CrearRolesHerenciaDto dto[^{]*\{\s*if \(FueraDeAlcance\(dto\.IdTenant\)\) return NotFound\(\);"), fuente);
        Assert.Contains("_service.ObtenerHijosAsync(idRolPadre, AlcanceTenant, ct)", fuente);
        Assert.Contains("_service.ObtenerPadresAsync(idRolHijo, AlcanceTenant, ct)", fuente);
        Assert.Contains("_service.EliminarAsync(id, AlcanceTenant, ct)", fuente);
    }

    [Fact]
    public void RolesPoliticasPwd_escribe_solo_con_POLITICASPWD_EDITAR()
    {
        var fuente = Leer("RolesPoliticasPwdController.cs");

        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""POLITICASPWD_VER""\)\]\s*public class RolesPoliticasPwdController : BaseApiController\b"), fuente);
        Assert.Equal(
            new List<(string, string?)> { ("Post", "POLITICASPWD_EDITAR"), ("Post", "POLITICASPWD_EDITAR") },
            AccionesDeEscritura(fuente));
    }
}
