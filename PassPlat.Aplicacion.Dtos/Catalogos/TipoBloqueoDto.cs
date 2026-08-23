namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class TipoBloqueoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsTemporal { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
}
