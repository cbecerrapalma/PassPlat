namespace PassPlat.Aplicacion.Dtos.Contexto;

public class DispDto
{
    public int Id { get; set; }
    public int IdTipoDisp { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public DateTime FecPrimerReg { get; set; }
    public DateTime? UltActividad { get; set; }
    public int CantidadLogins { get; set; }
    public string? IP { get; set; }
    public string? Pais { get; set; }
    public string? Navegador { get; set; }
    public string? SO { get; set; }
    public string? ProveedorAuth { get; set; }
    public string? TipoDispNombre { get; set; }
}
