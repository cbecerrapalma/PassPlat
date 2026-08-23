namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoDisp
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsMovil { get; set; }

    public ICollection<Disp> Disp { get; set; } = [];

    public static TipoDisp Crear(string nombre, bool esMovil, string? descripcion = null)
    {
        return new TipoDisp
        {
            Nombre = nombre,
            EsMovil = esMovil,
            Descripcion = descripcion
        };
    }
}
