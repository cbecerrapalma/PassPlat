using CBP.Results;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services;

public interface IFederacionService
{
    Task<Result<FederacionEstadisticasDto>> ObtenerEstadisticasAsync(int idTenant, CancellationToken ct = default);
}

public class FederacionService : IFederacionService
{
    private readonly IIdenExtRepository _idenExtRepo;
    private readonly IProvIdenRepository _provIdenRepo;
    private readonly IAudIdenExtRepository _audIdenExtRepo;

    public FederacionService(IIdenExtRepository idenExtRepo, IProvIdenRepository provIdenRepo, IAudIdenExtRepository audIdenExtRepo)
    {
        _idenExtRepo = idenExtRepo;
        _provIdenRepo = provIdenRepo;
        _audIdenExtRepo = audIdenExtRepo;
    }

    public async Task<Result<FederacionEstadisticasDto>> ObtenerEstadisticasAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var desgloseResult = await _idenExtRepo.ObtenerDesglosePorProveedorAsync(idTenant, ct);
            if (desgloseResult.IsFailure)
                return Result<FederacionEstadisticasDto>.Failure(desgloseResult.Error!);

            var totalProveedoresResult = await _provIdenRepo.ContarActivosAsync(ct);
            if (totalProveedoresResult.IsFailure)
                return Result<FederacionEstadisticasDto>.Failure(totalProveedoresResult.Error!);

            var ultimasActividadesResult = await _audIdenExtRepo.ObtenerPorTenantAsync(idTenant, 10, ct);
            if (ultimasActividadesResult.IsFailure)
                return Result<FederacionEstadisticasDto>.Failure(ultimasActividadesResult.Error!);

            var desglose = desgloseResult.Value
                .Select(g => new ProveedorEstadisticasDto
                {
                    IdProvIden = g.IdProvIden,
                    Codigo = g.Codigo,
                    Nombre = g.Nombre,
                    Icono = g.Icono,
                    TotalVinculadas = g.TotalVinculadas
                })
                .ToList();

            var totalIdentidades = desglose.Sum(d => d.TotalVinculadas);

            var ultimasActividades = ultimasActividadesResult.Value
                .Select(a => new UltimaActividadFederacionDto
                {
                    Id = a.Id,
                    Evento = a.Evento,
                    Resultado = a.Resultado,
                    Detalle = a.Detalle,
                    ProveedorNombre = a.ProvIden != null ? a.ProvIden.Nombre : null,
                    ProveedorCodigo = a.ProvIden != null ? a.ProvIden.Codigo : null,
                    FecEvento = a.FecEvento
                })
                .ToList();

            return Result<FederacionEstadisticasDto>.Success(new FederacionEstadisticasDto
            {
                TotalIdentidadesVinculadas = totalIdentidades,
                TotalProveedoresActivos = totalProveedoresResult.Value,
                DesglosePorProveedor = desglose,
                UltimasActividades = ultimasActividades
            });
        }
        catch (Exception ex)
        {
            return Result<FederacionEstadisticasDto>.Failure("DB_ERROR", ex.Message);
        }
    }
}
