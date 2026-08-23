using CBP.Results;
using CBP.Services.Abstractions;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;

namespace PassPlat.Aplicacion.Services;

public interface IMaintenanceService : ICustomService
{
    Task<Result<PurgeResult>> PurgeDatosAntiguosAsync(int diasRetencion, CancellationToken ct = default);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _maintenanceRepo;

    public MaintenanceService(IMaintenanceRepository maintenanceRepo) => _maintenanceRepo = maintenanceRepo;

    public async Task<Result<PurgeResult>> PurgeDatosAntiguosAsync(int diasRetencion, CancellationToken ct = default)
    {
        var result = await _maintenanceRepo.PurgeDatosAntiguosAsync(diasRetencion, ct);
        return result;
    }
}
