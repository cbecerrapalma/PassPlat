using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S109 · D6/D8 — el historial de envíos gana un GET aditivo protegido por EMAIL_LOG_VER y acotado
/// al tenant de la sesión, y las políticas del catálogo siguen como las fijó S102.
/// </summary>
public sealed class S109HistorialControladorTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string rutaRelativa) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), rutaRelativa));

    private static string LeerControlador(string controlador) =>
        Leer(Path.Combine("PassPlat.WebAPI", "Controllers", controlador));

    [Fact]
    public void GET_api_EmailLog_exige_EMAIL_LOG_VER_y_acota_por_AlcanceTenant()
    {
        var fuente = LeerControlador("EmailLogController.cs");

        Assert.Matches(
            new Regex(@"\[HttpGet\]\s*\[Authorize\(Policy = ""EMAIL_LOG_VER""\)\]\s*public async Task<IActionResult> GetRecientes\([^)]*\)\s*=>\s*FromResultQuery\(await _service\.ObtenerRecientesAsync\(User\.AlcanceTenant\(_tenantContext\)"),
            fuente);
        // S135 · D1 — pendientes y usuario/{idUsuario} se eliminaron (sin uso)
        Assert.DoesNotMatch(new Regex(@"\[HttpGet\(""pendientes""\)\]"), fuente);
        Assert.DoesNotMatch(new Regex(@"\[HttpGet\(""usuario/\{idUsuario\}""\)\]"), fuente);
    }

    [Fact]
    public void Politicas_de_RolesHerencia_Modulos_y_AppsModulos_no_cambian()
    {
        var herencia = LeerControlador("RolesHerenciaController.cs");
        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""ROLES_VER""\)\]\s*public class RolesHerenciaController : BaseApiController\b"), herencia);
        Assert.Equal(2, Regex.Matches(herencia, @"\[Authorize\(Policy = ""ROLES_EDITAR""\)\]").Count);

        var modulos = LeerControlador("ModulosController.cs");
        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""PERMISOS_VER""\)\]\s*public class ModulosController : BaseApiController\b"), modulos);
        Assert.Contains("[Authorize(Policy = \"PERMISOS_CREAR\")]", modulos);
        Assert.Contains("[Authorize(Policy = \"PERMISOS_EDITAR\")]", modulos);

        var apps = LeerControlador("AppsModulosController.cs");
        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""APPS_VER""\)\]\s*public class AppsModulosController : BaseApiController\b"), apps);
        Assert.Equal(2, Regex.Matches(apps, @"\[Authorize\(Policy = ""APPS_EDITAR""\)\]").Count);

        var candadoS102 = Leer(Path.Combine("PassPlat.CBP.Architecture.Tests", "S102PermisosControladoresTests.cs"));
        Assert.Contains("RolesHerenciaController : BaseApiController", candadoS102);
        Assert.Contains("Policy = \"\"ROLES_VER\"\"", candadoS102);
    }
}
