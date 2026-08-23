namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ResultadoAccesoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsExitoso { get; set; }
}
