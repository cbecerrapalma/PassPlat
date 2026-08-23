namespace PassPlat.Aplicacion.Dtos.Core;

public class FederacionEstadisticasDto
{
    public int TotalIdentidadesVinculadas { get; set; }
    public int TotalProveedoresActivos { get; set; }
    public List<ProveedorEstadisticasDto> DesglosePorProveedor { get; set; } = [];
    public List<UltimaActividadFederacionDto> UltimasActividades { get; set; } = [];
}

public class ProveedorEstadisticasDto
{
    public int IdProvIden { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Icono { get; set; }
    public int TotalVinculadas { get; set; }
}

public class UltimaActividadFederacionDto
{
    public long Id { get; set; }
    public string Evento { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? ProveedorCodigo { get; set; }
    public DateTime FecEvento { get; set; }
}
