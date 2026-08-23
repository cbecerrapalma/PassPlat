namespace PassPlat.Aplicacion.Dtos.Core;

public class EmailLogDto
{
    public long Id { get; set; }
    public int? IdTenant { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdTemplate { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Proveedor { get; set; }
    public string? MsgIdExterno { get; set; }
    public byte Intentos { get; set; }
    public DateTime? FecEnvio { get; set; }
    public DateTime? FecUltIntento { get; set; }
    public string? ErrorDetalle { get; set; }
    public DateTime FecCrea { get; set; }
}
