namespace PassPlat.Dominio.Entities.Catalogos;

public class EstadoMFA
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<MFA> MFA { get; set; } = [];

    public static EstadoMFA Crear(string codigo, string nombre, string? descripcion = null)
    {
        return new EstadoMFA
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion
        };
    }
}
