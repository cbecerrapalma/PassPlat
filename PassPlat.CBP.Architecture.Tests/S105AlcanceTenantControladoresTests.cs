using System.Text.RegularExpressions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S105 · P63, P61, P66 — un solo ayudante de alcance de tenant; el historial de correos y los Accesos
/// de un usuario quedan acotados al tenant de la sesión, salvo para el propio usuario y el sistema.
/// </summary>
public sealed class S105AlcanceTenantControladoresTests
{
    private static readonly string[] Controladores =
        ["AuditoriaPwd", "ConfProvIden", "Grupos", "GruposUsuarios", "IdenExt", "TenantEmailAccounts", "UsuariosPermisos", "RolesHerencia"];

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(string carpeta, string archivo) =>
        File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", carpeta, archivo));

    [Fact]
    public void Los_ocho_controladores_usan_el_ayudante_comun()
    {
        foreach (var controlador in Controladores)
        {
            var fuente = Leer("Controllers", controlador + "Controller.cs");
            Assert.Contains("private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);", fuente);
            Assert.DoesNotContain("? null : _tenantContext.CurrentId", fuente);
        }
    }

    [Fact]
    public void El_ayudante_define_sistema_propio_y_alcance()
    {
        var fuente = Leer("Auth", "AlcanceTenantExtensions.cs");

        Assert.Contains("public static class AlcanceTenantExtensions", fuente);
        Assert.Contains("user.HasClaim(\"is_system\", \"true\")", fuente);
        Assert.Contains("user.EsSistema() ? null : tenantContext.CurrentId", fuente);
        Assert.Contains("user.AlcanceTenant(tenantContext) is int propio && propio != idTenant", fuente);
        Assert.Contains("user.EsPropio(idUsuario) ? null : user.AlcanceTenant(tenantContext)", fuente);
    }

    [Fact]
    public void EmailLog_exige_EMAIL_LOG_VER_y_acota_por_tenant()
    {
        var fuente = Leer("Controllers", "EmailLogController.cs");

        Assert.Matches(new Regex(@"public class EmailLogController : BaseApiController\b"), fuente);
        Assert.Matches(new Regex(@"\[Authorize\(Policy = ""EMAIL_LOG_VER""\)\]\s*public async Task<IActionResult> GetRecientes"), fuente);
        Assert.Contains("_service.ObtenerRecientesAsync(User.AlcanceTenant(_tenantContext)", fuente);
        // S135 · D1 — pendientes y usuario/{idUsuario} se eliminaron (sin uso)
        Assert.DoesNotContain("GetPendientes", fuente);
        Assert.DoesNotContain("ObtenerPorUsuarioAsync", fuente);
        Assert.DoesNotContain("BadRequest(new { error", fuente);
    }

    [Fact]
    public void Accesos_por_usuario_acota_por_tenant_salvo_el_propio()
    {
        var fuente = Leer("Controllers", "AccesosController.cs");

        Assert.Contains("!User.EsPropio(idUsuario) && !User.EsSistema() && !User.HasClaim(\"permiso\", \"USUARIOS_VER\")", fuente);
        Assert.Contains("_service.ObtenerAccesosUsuarioAsync(idUsuario, User.AlcanceTenantDe(_tenantContext, idUsuario), ct)", fuente);
        Assert.DoesNotContain("hasUsuariosVer", fuente);
    }
}
