namespace PassPlat.Dominio.Entities.Catalogos;

public class Grupo
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public Tenant? Tenant { get; set; }
    public ICollection<GrupoUsuario> GruposUsuarios { get; set; } = [];

    public static Grupo Crear(int idTenant, string codigo, string nombre, string? descripcion = null)
    {
        return new Grupo
        {
            IdTenant = idTenant,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
