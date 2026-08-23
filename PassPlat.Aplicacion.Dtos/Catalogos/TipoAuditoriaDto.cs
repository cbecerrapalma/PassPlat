namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class TipoAuditoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public DateTime FecCrea { get; set; }
}
