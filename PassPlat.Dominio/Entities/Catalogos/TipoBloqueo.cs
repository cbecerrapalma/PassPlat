namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoBloqueo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsTemporal { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<Bloqueo> Bloqueos { get; set; } = [];

    public static TipoBloqueo Crear(string nombre, bool esTemporal = true, string? descripcion = null)
    {
        return new TipoBloqueo
        {
            Nombre = nombre,
            EsTemporal = esTemporal,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
