namespace PassPlat.Aplicacion.Dtos.Core;

public class AccesoDto
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public int IdRol { get; set; }
    public bool Activo { get; set; }
    public DateTime FecAsignacion { get; set; }
    public string? AppNombre { get; set; }
    public string? RolNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? TenantNombre { get; set; }
}

public class AsignarAccesoDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public int IdRol { get; set; }
}
