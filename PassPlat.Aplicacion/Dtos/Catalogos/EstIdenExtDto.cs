namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class EstIdenExtDto
{
    public byte Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public string? Color { get; init; }
    public short Orden { get; init; }
    public bool Activo { get; init; }
    public DateTime? FecCrea { get; init; }
    public DateTime? FecMod { get; init; }
}
