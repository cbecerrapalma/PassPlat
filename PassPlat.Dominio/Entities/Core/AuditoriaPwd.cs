namespace PassPlat.Dominio.Entities.Core;

public class AuditoriaPwd
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int? IdTenant { get; set; }
    public int? IdApp { get; set; }
    public int IdTipoAccion { get; set; }
    public int? IdUsrEjecutor { get; set; }
    public int? IdDisp { get; set; }
    public int? IdAgente { get; set; }
    public int? IdIP { get; set; }
    public long? IdHistPwd { get; set; }
    public string? Detalles { get; set; }
    public int? NivelRiesgo { get; set; }
    public string? Metadata { get; set; }
    public DateTime? FecAccion { get; set; }
    public DateTime? FecRetencion { get; private set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }
    public App? App { get; set; }
    public TipoAuditoria? TipoAccion { get; set; }
    public Usuario? UsrEjecutor { get; set; }
    public Disp? Disp { get; set; }
    public UserAgent? Agente { get; set; }
    public IP? DireccionIP { get; set; }
    public HistorialPwd? HistPwd { get; set; }

    public static AuditoriaPwd Crear(int idUsuario, int idTipoAccion, int? idUsrEjecutor = null)
    {
        return new AuditoriaPwd
        {
            IdUsuario = idUsuario,
            IdTipoAccion = idTipoAccion,
            IdUsrEjecutor = idUsrEjecutor,
            FecAccion = DateTime.Now
        };
    }
}
