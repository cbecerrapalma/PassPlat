namespace PassPlat.Dominio.Entities.Catalogos;

public class TipAsigPermiso
{
    public byte Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public ICollection<UsuarioPermiso> UsuariosPermisos { get; set; } = [];

    public static TipAsigPermiso Crear(string nombre)
    {
        return new TipAsigPermiso
        {
            Nombre = nombre
        };
    }
}
