namespace PassPlat.Aplicacion.Dtos.Core;

public class EmailTemplateHistorialDto
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
    public string? UsuarioNombre { get; set; }
}
