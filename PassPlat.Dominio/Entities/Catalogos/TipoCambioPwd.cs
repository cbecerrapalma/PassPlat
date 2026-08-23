namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoCambioPwd
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<HistorialPwd> HistorialPwd { get; set; } = [];

    public static TipoCambioPwd Crear(string codigo, string nombre, string? descripcion = null)
    {
        return new TipoCambioPwd
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion
        };
    }
}
