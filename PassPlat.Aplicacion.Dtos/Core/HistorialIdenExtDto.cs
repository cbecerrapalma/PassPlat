namespace PassPlat.Aplicacion.Dtos.Core;

public class HistorialIdenExtDto
{
    public long Id { get; init; }
    public int IdTenant { get; init; }
    public int IdUsuario { get; init; }
    public long IdIdenExt { get; init; }
    public int IdProvIden { get; init; }
    public string TipoCambio { get; init; } = string.Empty;
    public string? ValorAnterior { get; init; }
    public string? ValorNuevo { get; init; }
    public int? RealizadoPor { get; init; }
    public bool EsAutomatico { get; init; }
    public Guid? CorrelationId { get; init; }
    public DateTime FecCambio { get; init; }
    public string? UsuarioNombre { get; init; }
    public string? ProvIdenNombre { get; init; }
    public string? RealizadoPorNombre { get; init; }
}
