namespace PassPlat.Dominio.Entities.Core;

public class UsuarioPermiso
{
    public int Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdPermiso { get; set; }
    public int IdTenant { get; set; }
    public int? IdApp { get; set; }
    public byte IdTipoAsig { get; set; } = 1;
    public bool Activo { get; set; } = true;
    public DateTime? FecInicio { get; set; }
    public DateTime? FecFin { get; set; }
    public string? Motivo { get; set; }
    public int IdUsrCrea { get; set; }
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public int IdUsrMod { get; set; }
    public DateTime? FecMod { get; set; }

    public Usuario? Usuario { get; set; }
    public Permiso? Permiso { get; set; }
    public App? App { get; set; }
    public Tenant? Tenant { get; set; }
    public TipAsigPermiso? TipAsigPermiso { get; set; }
    public Usuario? UsrCrea { get; set; }
    public Usuario? UsrMod { get; set; }

    public static UsuarioPermiso Crear(int idUsuario, int idPermiso, int idTenant, byte idTipoAsig, int idUsrCrea, int? idApp = null, string? motivo = null)
    {
        return new UsuarioPermiso
        {
            IdUsuario = idUsuario,
            IdPermiso = idPermiso,
            IdTenant = idTenant,
            IdTipoAsig = idTipoAsig,
            IdApp = idApp,
            Motivo = motivo,
            Activo = true,
            IdUsrCrea = idUsrCrea,
            IdUsrMod = idUsrCrea,
            FecCrea = DateTime.Now
        };
    }
}
