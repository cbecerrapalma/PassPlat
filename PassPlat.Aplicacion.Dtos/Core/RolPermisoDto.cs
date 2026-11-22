namespace PassPlat.Aplicacion.Dtos.Core;

public class RolPermisoDto
{
    public int Id { get; set; }
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime FecMod { get; set; }
    public int? IdUsrMod { get; set; }
    public string? PermisoCodigo { get; set; }
    public string? PermisoNombre { get; set; }
    public string? Modulo { get; set; }
}

public class AsignarPermisoDto
{
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }
}

// S106 · D1 — cambios de permisos de un rol en una sola petición (todo o nada)
public class AplicarPermisosRolDto
{
    public List<CambioPermisoDto> Cambios { get; set; } = [];
}

public class ResultadoAplicarPermisosRolDto
{
    public int Agregados { get; set; }
    public int Quitados { get; set; }
    public int SinCambio { get; set; }
}
