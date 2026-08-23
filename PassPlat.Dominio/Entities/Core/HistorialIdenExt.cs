namespace PassPlat.Dominio.Entities.Core;

public class HistorialIdenExt
{
    public long Id { get; set; }
    public int IdTenant { get; set; }
    public int IdUsuario { get; set; }
    public long IdIdenExt { get; set; }
    public int IdProvIden { get; set; }
    public string TipoCambio { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public int? RealizadoPor { get; set; }
    public bool EsAutomatico { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime FecCambio { get; set; } = DateTime.Now;

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }
    public IdenExt? IdenExt { get; set; }
    public Catalogos.ProvIden? ProvIden { get; set; }
    public Usuario? RealizadoPorNav { get; set; }

    public static HistorialIdenExt Crear(int idTenant, int idUsuario, long idIdenExt, int idProvIden, string tipoCambio, string? valorAnterior = null, string? valorNuevo = null, int? realizadoPor = null, bool esAutomatico = false, Guid? correlationId = null)
    {
        return new HistorialIdenExt
        {
            IdTenant = idTenant,
            IdUsuario = idUsuario,
            IdIdenExt = idIdenExt,
            IdProvIden = idProvIden,
            TipoCambio = tipoCambio,
            ValorAnterior = valorAnterior,
            ValorNuevo = valorNuevo,
            RealizadoPor = realizadoPor,
            EsAutomatico = esAutomatico,
            CorrelationId = correlationId,
            FecCambio = DateTime.Now
        };
    }
}
