namespace PassPlat.Dominio.Entities.Catalogos;

public class RolesHerencia
{
    public int Id { get; set; }
    public int IdRolHijo { get; set; }
    public int IdRolPadre { get; set; }
    public int IdTenant { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public Rol? RolHijo { get; set; }
    public Rol? RolPadre { get; set; }
    public Tenant? Tenant { get; set; }

    public static RolesHerencia Crear(int idRolHijo, int idRolPadre, int idTenant)
    {
        return new RolesHerencia
        {
            IdRolHijo = idRolHijo,
            IdRolPadre = idRolPadre,
            IdTenant = idTenant,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
