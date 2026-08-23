namespace PassPlat.Dominio.Entities.Catalogos;

public class TipoAuditoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<AuditoriaPwd> AuditoriaPwd { get; set; } = [];

    public static TipoAuditoria Crear(string nombre, bool activo = true, string? descripcion = null)
    {
        return new TipoAuditoria
        {
            Nombre = nombre,
            Activo = activo,
            Descripcion = descripcion,
            FecCrea = DateTime.Now
        };
    }
}
