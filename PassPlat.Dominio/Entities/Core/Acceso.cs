namespace PassPlat.Dominio.Entities.Core;

public class Acceso
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdApp { get; set; }
    public int IdRol { get; set; }
    public int? IdUsuarioTenant { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecAsignacion { get; set; } = DateTime.Now;

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public Rol? Rol { get; set; }
    public UsuarioTenant? UsuarioTenant { get; set; }

    public static Acceso Crear(int idUsuario, int idTenant, int idApp, int idRol)
    {
        return new Acceso
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdApp = idApp,
            IdRol = idRol,
            Activo = true,
            FecAsignacion = DateTime.Now
        };
    }

    public void Revocar()
    {
        Activo = false;
    }

    public void Activar()
    {
        Activo = true;
        FecAsignacion = DateTime.Now;
    }
}
