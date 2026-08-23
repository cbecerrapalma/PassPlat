namespace PassPlat.Dominio.Entities.Core;

public class EmailTemplateHistorial
{
    public long Id { get; set; }
    public int IdTemplate { get; set; }
    public int Version { get; set; }
    public string Asunto { get; set; } = string.Empty;
    public string CuerpoHtml { get; set; } = string.Empty;
    public string? CuerpoTexto { get; set; }
    public DateTime FecPublicacion { get; set; }
    public int IdUsrPublico { get; set; }
    public string? Motivo { get; set; }

    public EmailTemplate? Template { get; set; }
    public Usuario? Usuario { get; set; }

    public static EmailTemplateHistorial Crear(int idTemplate, int version, string asunto, string cuerpoHtml, int idUsrPublico, string? cuerpoTexto = null, string? motivo = null)
    {
        return new EmailTemplateHistorial
        {
            IdTemplate = idTemplate,
            Version = version,
            Asunto = asunto,
            CuerpoHtml = cuerpoHtml,
            CuerpoTexto = cuerpoTexto,
            FecPublicacion = DateTime.Now,
            IdUsrPublico = idUsrPublico,
            Motivo = motivo
        };
    }
}
