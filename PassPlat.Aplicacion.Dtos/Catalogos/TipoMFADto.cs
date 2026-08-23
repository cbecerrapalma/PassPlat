namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class TipoMFADto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public byte Prioridad { get; set; }
    public bool ReqConfig { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
}
