namespace PassPlat.Aplicacion.Dtos.Core;

public class UsuarioPermisoDto
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdPermiso { get; set; }
    public int IdTenant { get; set; }
    public int? IdApp { get; set; }
    public byte IdTipoAsig { get; set; }
    public bool Activo { get; set; }
    public DateTime? FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
    public string? Motivo { get; set; }
    public int IdUsrCrea { get; set; }
    public DateTime FecCrea { get; set; }
    public int IdUsrMod { get; set; }
    public DateTime? FecMod { get; set; }
    public string? PermisoCodigo { get; set; }
    public string? PermisoNombre { get; set; }
    public string? UsuarioNombre { get; set; }
}

public class CrearUsuarioPermisoDto
{
    public int IdUsuario { get; set; }
    public int IdPermiso { get; set; }
    public int IdTenant { get; set; }
    public int? IdApp { get; set; }
    public byte IdTipoAsig { get; set; } = 1;
    public string? Motivo { get; set; }
    public DateTime? FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
}
