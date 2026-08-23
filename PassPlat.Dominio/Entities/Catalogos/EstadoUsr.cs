namespace PassPlat.Dominio.Entities.Catalogos;

public class EstadoUsr
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FecCrea { get; set; } = DateTime.Now;

    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<UsuarioTenant> UsuarioTenants { get; set; } = [];

    public static EstadoUsr Crear(string codigo, string nombre, string? descripcion = null)
    {
        return new EstadoUsr
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
