using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IEstadoMFAService : IServiceAsync<EstadoMFA, EstadoMFADto>
{
    Task<Result<IReadOnlyList<EstadoMFADto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<EstadoMFADto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class EstadoMFAService : ServiceAsync<EstadoMFA, EstadoMFADto>, IEstadoMFAService
{
    public EstadoMFAService(EstadoMFARepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<EstadoMFADto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<EstadoMFADto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<EstadoMFADto?>.Failure(result.Error!);
        return Result<EstadoMFADto?>.Success(Mapper.Map<EstadoMFADto>(result.Value));
    }
}
