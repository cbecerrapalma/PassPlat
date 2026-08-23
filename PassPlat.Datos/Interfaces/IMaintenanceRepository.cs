using CBP.Results;
using PassPlat.Datos.SPResults;

namespace PassPlat.Datos.Interfaces;

public interface IMaintenanceRepository
{
    Task<Result<PurgeResult>> PurgeDatosAntiguosAsync(int diasRetencion = 365, CancellationToken ct = default);
}
