namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S103 · D6 — <c>api/PermisosEfectivos</c> dejó de ser legible por cualquier autenticado:
/// propio, con PERMISOS_VER o de sistema, y un no-sistema solo en su tenant.
/// </summary>
public sealed class S103PermisosEfectivosControladorTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Fact]
    public void PermisosEfectivos_exige_propio_o_PERMISOS_VER_y_acota_por_tenant()
    {
        var fuente = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.WebAPI", "Controllers", "PermisosEfectivosController.cs"));

        Assert.Contains("[Authorize]", fuente);
        Assert.Contains("User.HasClaim(\"permiso\", \"PERMISOS_VER\")", fuente);
        Assert.Contains("User.HasClaim(\"is_system\", \"true\")", fuente);
        Assert.Contains("Forbid()", fuente);
        Assert.Contains("ITenantContext", fuente);
        Assert.Contains("_tenantContext.CurrentId", fuente);
        Assert.Contains("NotFound()", fuente);
        Assert.Contains("BadRequest(", fuente);
        // la ruta y el SP no cambian
        Assert.Contains("[HttpGet(\"usuario/{idUsuario}\")]", fuente);
        Assert.Contains("\"SP_Permisos_Usuario_Efectivos\"", fuente);
    }
}
