namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class RolesHerenciaDto
{
    public int Id { get; set; }
    public int IdRolHijo { get; set; }
    public int IdRolPadre { get; set; }
    public int IdTenant { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
    public string? RolHijoNombre { get; set; }
    public string? RolPadreNombre { get; set; }
    public string? TenantNombre { get; set; }
}

public class CrearRolesHerenciaDto
{
    public int IdRolHijo { get; set; }
    public int IdRolPadre { get; set; }
    public int IdTenant { get; set; }
}
