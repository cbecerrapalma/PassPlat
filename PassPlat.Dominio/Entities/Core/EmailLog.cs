namespace PassPlat.Dominio.Entities.Core;

public class EmailLog
{
    public long Id { get; set; }
    public int? IdTenant { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdApp { get; set; }
    public int? IdTemplate { get; set; }
    public int? IdEmailAccount { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Estado { get; set; } = "pendiente";
    public string? Proveedor { get; set; }
    public string? MsgIdExterno { get; set; }
    public byte Intentos { get; set; }
    public DateTime? FecEnvio { get; set; }
    public DateTime? FecUltIntento { get; set; }
    public string? ErrorDetalle { get; set; }
    public string? CorrelationId { get; set; }
    public string? ExtraJson { get; set; }
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }
    public App? App { get; set; }
    public EmailTemplate? Template { get; set; }
    public EmailAccount? EmailAccount { get; set; }

    public static EmailLog Crear(string destinatario, string asunto, int? idTenant = null, int? idUsuario = null, int? idApp = null, int? idTemplate = null, int? idEmailAccount = null, string? correlationId = null, string? extraJson = null, string? proveedor = null)
    {
        return new EmailLog
        {
            Destinatario = destinatario,
            Asunto = asunto,
            IdTenant = idTenant,
            IdUsuario = idUsuario,
            IdApp = idApp,
            IdTemplate = idTemplate,
            IdEmailAccount = idEmailAccount,
            CorrelationId = correlationId,
            ExtraJson = extraJson,
            Proveedor = proveedor,
            Estado = "pendiente",
            Intentos = 0,
            FecCrea = DateTime.Now
        };
    }
}
