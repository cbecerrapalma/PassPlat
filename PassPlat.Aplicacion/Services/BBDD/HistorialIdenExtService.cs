using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IHistorialIdenExtService : IServiceAsync<HistorialIdenExt, HistorialIdenExtDto>
{
    Task<Result<HistorialIdenExtDto>> RegistrarCambioAsync(int idTenant, int idUsuario, long idIdenExt, int idProvIden, string tipoCambio, string? valorAnterior = null, string? valorNuevo = null, int? realizadoPor = null, bool esAutomatico = false, Guid? correlationId = null, CancellationToken ct = default);
}

public class HistorialIdenExtService : ServiceAsync<HistorialIdenExt, HistorialIdenExtDto>, IHistorialIdenExtService
{
    private readonly IHistorialIdenExtRepository _repo;

    public HistorialIdenExtService(IHistorialIdenExtRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<HistorialIdenExtDto>> RegistrarCambioAsync(int idTenant, int idUsuario, long idIdenExt, int idProvIden, string tipoCambio, string? valorAnterior = null, string? valorNuevo = null, int? realizadoPor = null, bool esAutomatico = false, Guid? correlationId = null, CancellationToken ct = default)
    {
        var entity = HistorialIdenExt.Crear(idTenant, idUsuario, idIdenExt, idProvIden, tipoCambio, valorAnterior, valorNuevo, realizadoPor, esAutomatico, correlationId);
        var addResult = _repo.Add(entity);
        if (addResult.IsFailure)
            return Result<HistorialIdenExtDto>.Failure(addResult.Error!);
        return Result<HistorialIdenExtDto>.Success(Mapper.Map<HistorialIdenExtDto>(entity));
    }
}
