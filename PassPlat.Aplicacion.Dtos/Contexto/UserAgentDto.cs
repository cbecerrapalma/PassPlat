namespace PassPlat.Aplicacion.Dtos.Contexto;

public class UserAgentDto
{
    public int Id { get; set; }
    public string Agente { get; set; } = string.Empty;
    public string HashAgente { get; set; } = string.Empty;
    public string? Navegador { get; set; }
    public string? Version { get; set; }
    public string? SistemaOperativo { get; set; }
    public bool? EsMovil { get; set; }
    public DateTime FecPrimerUso { get; set; }
    public DateTime? FecUltUso { get; set; }
    public int VecesUsado { get; set; }
}
