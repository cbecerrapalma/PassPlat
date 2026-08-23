namespace PassPlat.Dominio.Entities.Catalogos;

public class ResultadoAcceso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsExitoso { get; set; }

    public ICollection<IntentoAcceso> IntentosAcceso { get; set; } = [];

    public static ResultadoAcceso Crear(string nombre, bool esExitoso, string? descripcion = null)
    {
        return new ResultadoAcceso
        {
            Nombre = nombre,
            EsExitoso = esExitoso,
            Descripcion = descripcion
        };
    }
}
