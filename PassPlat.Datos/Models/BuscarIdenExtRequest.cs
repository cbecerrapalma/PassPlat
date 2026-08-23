namespace PassPlat.Datos.Models;

public class BuscarIdenExtRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? IdEstado { get; init; }
    public int? IdProveedor { get; init; }
    public int? IdTenant { get; init; }
    public bool? ConMFA { get; init; }
    public bool? SoloExpirados { get; init; }
    public string? TextoLibre { get; init; }
    public string? UsuarioNombre { get; init; }
}
