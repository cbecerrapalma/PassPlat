namespace PassPlat.Dominio.Entities.Core;

public class Notificacion
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string TipoNotif { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public bool Leida { get; set; }
    public DateTime FecCrea { get; set; } = DateTime.Now;
    public DateTime? FecLeida { get; set; }

    public Usuario? Usuario { get; set; }
    public Tenant? Tenant { get; set; }

    public static Notificacion Crear(int idUsuario, int idTenant, string tipoNotif, string asunto, string? mensaje = null)
    {
        return new Notificacion
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            TipoNotif = tipoNotif,
            Asunto = asunto,
            Mensaje = mensaje,
            FecCrea = DateTime.Now
        };
    }

    public void MarcarLeida()
    {
        Leida = true;
        FecLeida = DateTime.Now;
    }
}
