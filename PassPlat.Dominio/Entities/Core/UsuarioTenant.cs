namespace PassPlat.Dominio.Entities.Core;

public class UsuarioTenant
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public int IdEstado { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecAlta { get; set; } = DateTime.Now;
    public DateTime? FecMod { get; set; }
    public int? IdUsrMod { get; set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public EstadoUsr? Estado { get; set; }
    public ICollection<Acceso> Accesos { get; set; } = [];

    public static UsuarioTenant Crear(int idUsuario, int idTenant, int idEstado)
    {
        return new UsuarioTenant
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            IdEstado = idEstado,
            Activo = true,
            FecAlta = DateTime.Now
        };
    }

    public void Desactivar()
    {
        Activo = false;
        FecMod = DateTime.Now;
    }

    public void Activar()
    {
        Activo = true;
        FecMod = DateTime.Now;
    }
}
