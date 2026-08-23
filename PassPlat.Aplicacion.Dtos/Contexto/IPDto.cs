namespace PassPlat.Aplicacion.Dtos.Contexto;

public class IPDto
{
    public int Id { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public byte TipoIP { get; set; }
    public string? Pais { get; set; }
    public string? Ciudad { get; set; }
    public bool EsSospechosa { get; set; }
    public DateTime FecPrimerUso { get; set; }
    public DateTime? UltUso { get; set; }
}
