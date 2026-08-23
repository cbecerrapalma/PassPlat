namespace PassPlat.Datos.SPResults;

public class PermisosUsuarioEfectivosResult
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public byte Orden { get; set; }
}
