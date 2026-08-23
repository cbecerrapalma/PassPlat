namespace PassPlat.Dominio.Entities.Catalogos;

public class Rol
{
    public int Id { get; set; }
    public int? IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public Tenant? Tenant { get; set; }
    public ICollection<Acceso> Accesos { get; set; } = [];
    public ICollection<RolPoliticaPwd> RolesPoliticasPwd { get; set; } = [];
    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];
    public ICollection<RolesHerencia> RolesHerenciaHijos { get; set; } = [];
    public ICollection<RolesHerencia> RolesHerenciaPadres { get; set; } = [];

    public static Rol Crear(string codigo, string nombre, int? idTenant = null, string? descripcion = null)
    {
        return new Rol
        {
            Codigo = codigo,
            Nombre = nombre,
            IdTenant = idTenant,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activo = false;
    }
}
