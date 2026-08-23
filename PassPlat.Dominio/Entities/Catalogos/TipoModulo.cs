namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoModulo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Modulo> Modulos { get; set; } = [];

    public static TipoModulo Crear(string codigo, string nombre, string? descripcion = null)
    {
        return new TipoModulo
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = true
        };
    }
}
