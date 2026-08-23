namespace PassPlat.Datos.Models;

public class DesgloseProveedorReadModel
{
    public int IdProvIden { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Icono { get; init; }
    public int TotalVinculadas { get; init; }
}
