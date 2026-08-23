namespace PassPlat.Dominio.Entities.Core;

public class RolPoliticaPwd
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdRol { get; set; }
    public int IdPolitica { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public DateTime FecMod { get; set; } = DateTime.Now;
    public int? IdUsrMod { get; set; }

    public Tenant? Tenant { get; set; }
    public Rol? Rol { get; set; }
    public PoliticaPwd? Politica { get; set; }
    public Usuario? UsrMod { get; set; }

    public static RolPoliticaPwd Crear(int idTenant, int idRol, int idPolitica)
    {
        return new RolPoliticaPwd
        {
            IdTenant = idTenant,
            IdRol = idRol,
            IdPolitica = idPolitica,
            Activo = true,
            FecCrea = DateTime.Now,
            FecMod = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activo = false;
        FecMod = DateTime.Now;
    }
}
