namespace PassPlat.Dominio.Entities.Core;

public class RolPermiso
{
    public int Id { get; set; }
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public DateTime FecMod { get; set; } = DateTime.Now;
    public int? IdUsrMod { get; set; }

    public Rol? Rol { get; set; }
    public Permiso? Permiso { get; set; }
    public Usuario? UsrMod { get; set; }

    public static RolPermiso Crear(int idRol, int idPermiso)
    {
        return new RolPermiso
        {
            IdRol = idRol,
            IdPermiso = idPermiso,
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
