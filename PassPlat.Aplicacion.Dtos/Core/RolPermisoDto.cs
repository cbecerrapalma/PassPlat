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
