using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;

namespace PassPlat.Datos.Repositories;

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public MaintenanceRepository(IUnitOfWorkAsync uow)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<PurgeResult>> PurgeDatosAntiguosAsync(int diasRetencion = 365, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@DiasRetencion", diasRetencion)
        };

        var result = await _rawQuery.QuerySPAsync<PurgeResult>("SP_Purge_DatosAntiguos", parameters, ct);
        if (!result.IsSuccess)
            return Result<PurgeResult>.Failure(result.Error!);

        var purgeResult = result.Value.FirstOrDefault();
        return purgeResult != null
            ? Result<PurgeResult>.Success(purgeResult)
            : Result<PurgeResult>.Failure("SP_NO_RESULT", "Sin resultado del SP");
    }
}
