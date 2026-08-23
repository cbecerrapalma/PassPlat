using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoMFAService : IServiceAsync<TipoMFA, TipoMFADto>
{
    Task<Result<IReadOnlyList<TipoMFADto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TipoMFADto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TipoMFADto>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class TipoMFAService : ServiceAsync<TipoMFA, TipoMFADto>, ITipoMFAService
{
    private readonly TipoMFARepository _repo;

    public TipoMFAService(TipoMFARepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<TipoMFADto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<TipoMFADto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TipoMFADto?>.Failure(result.Error!);
        return Result<TipoMFADto?>.Success(Mapper.Map<TipoMFADto>(result.Value));
    }

    public async Task<Result<IReadOnlyList<TipoMFADto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<TipoMFADto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<TipoMFADto>>.Success(Mapper.Map<IReadOnlyList<TipoMFADto>>(listResult.Value));
    }
}
