namespace PassPlat.Dominio.Entities.Catalogos;

public class Permiso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdModulo { get; set; }
    public byte Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public Modulo? Modulo { get; set; }
    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];

    public static Permiso Crear(string codigo, string nombre, int idModulo, string? descripcion = null, byte orden = 0)
    {
        return new Permiso
        {
            Codigo = codigo,
            Nombre = nombre,
            IdModulo = idModulo,
            Descripcion = descripcion,
            Orden = orden,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
