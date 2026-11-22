namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S107 · D1-D2 (P67) — los controladores pasan el ejecutor a las escrituras de permisos, donde el servicio
/// rechaza la autoedición (AUTOEDICION).
/// </summary>
public sealed class S107AutoedicionControladoresTests
{
    private static string Leer(string controlador)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        var raiz = dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
        return File.ReadAllText(Path.Combine(raiz, "PassPlat.WebAPI", "Controllers", controlador));
    }

    [Fact]
    public void Escrituras_de_permisos_reciben_el_ejecutor()
    {
        var usuarios = Leer("UsuariosPermisosController.cs");
        Assert.Contains("_service.AplicarPermisosAsync(dto, User.IdUsuario() ?? 0, User.EsSistema(), ct)", usuarios);
        Assert.Contains("_service.RevocarPermisoAsync(id, AlcanceTenant, User.IdUsuario() ?? 0, ct)", usuarios);

        var permisos = Leer("PermisosController.cs");
        Assert.Contains("_rolPermisoService.AsignarPermisoAsync(dto, AlcanceTenant, User.IdUsuario() ?? 0, ct)", permisos);
        Assert.Contains("_rolPermisoService.DesasignarPermisoAsync(idRol, idPermiso, AlcanceTenant, User.IdUsuario() ?? 0, ct)", permisos);
        Assert.Contains("_rolPermisoService.AplicarPermisosAsync(idRol, dto.Cambios ?? [], AlcanceTenant, User.IdUsuario() ?? 0, ct)", permisos);
    }
}
