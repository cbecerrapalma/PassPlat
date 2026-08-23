namespace PassPlat.Aplicacion.Dtos.Core;

public class NotificacionDto
{
    public long Id { get; set; }
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string TipoNotif { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public bool Leida { get; set; }
    public DateTime FecCrea { get; set; }
    public DateTime? FecLeida { get; set; }
}

public class CrearNotificacionDto
{
    public int IdUsuario { get; set; }
    public int IdTenant { get; set; }
    public string TipoNotif { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
}
