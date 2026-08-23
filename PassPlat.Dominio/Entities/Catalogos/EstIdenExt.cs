namespace PassPlat.Dominio.Entities.Catalogos;

public class EstIdenExt
{
    public byte Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Color { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public ICollection<Core.IdenExt> IdenExt { get; set; } = [];

    public static EstIdenExt Crear(byte id, string nombre, string? descripcion = null, string? color = null, short orden = 0)
    {
        return new EstIdenExt
        {
            Id = id,
            Nombre = nombre,
            Descripcion = descripcion,
            Color = color,
            Orden = orden,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
