namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class TipoCambioPwdDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}
