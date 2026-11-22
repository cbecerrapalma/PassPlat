namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S98 · P14 — la API no confía en el actor que manda el cliente: IdenExt y el registro de
/// auditoría lo toman del token, y la Web deja de mandarlo.
/// </summary>
public sealed class S98ActorDelTokenTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static string Leer(params string[] ruta) =>
        File.ReadAllText(Path.Combine([RaizDelRepo(), .. ruta]));

    [Fact]
    public void S98_iden_ext_toma_el_actor_del_token()
    {
        var fuente = Leer("PassPlat.WebAPI", "Controllers", "IdenExtController.cs");
        Assert.Contains("private int? IdUsuarioActual => User.IdUsuario();", fuente);
        Assert.Equal(4, fuente.Split("if (IdUsuarioActual is not int actor) return Unauthorized();").Length - 1);
        Assert.DoesNotContain("idUsuarioElimina", fuente);
        Assert.DoesNotContain("idUsuarioRevoca", fuente);
        Assert.DoesNotContain("idUsuarioAdmin", fuente);
        Assert.DoesNotContain("[FromQuery] int idUsuario", fuente);
    }

    [Fact]
    public void S98_auditoria_no_expone_registrar_desde_la_API()
    {
        // S135 · D1 — POST registrar se eliminó (sin uso): la auditoría la escribe solo el servicio
        var fuente = Leer("PassPlat.WebAPI", "Controllers", "AuditoriaPwdController.cs");
        Assert.DoesNotContain("[HttpPost(\"registrar\")]", fuente);
        Assert.DoesNotContain("IdUsrEjecutor", fuente);
    }

    [Theory]
    [InlineData("Usuarios", "Components", "UsuarioIdentidades.razor")]
    [InlineData("Federacion", "IdenExt", "Index.razor")]
    [InlineData("Federacion", "IdenExt", "IdenExtDetail.razor")]
    public void S98_web_no_manda_el_actor_a_iden_ext(string carpeta, string subcarpeta, string archivo)
    {
        var fuente = Leer("PassPlat.Web", "Pages", carpeta, subcarpeta, archivo);
        Assert.DoesNotContain("idUsuarioAdmin=", fuente);
        Assert.DoesNotContain("idUsuarioRevoca=", fuente);
        Assert.DoesNotContain("cambiar-principal?idUsuario=", fuente);
        Assert.DoesNotContain("/desvincular/{", fuente);
    }
}
