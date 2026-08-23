using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IEstIdenExtService : IServiceAsync<EstIdenExt, EstIdenExtDto>
{
    Task<Result<IReadOnlyList<EstIdenExtDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<EstIdenExtDto?>> ObtenerPorIdAsync(byte id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EstIdenExtDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<EstIdenExtDto?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default);
}

public class EstIdenExtService : ServiceAsync<EstIdenExt, EstIdenExtDto>, IEstIdenExtService
{
    private readonly IEstIdenExtRepository _repo;

    public EstIdenExtService(IEstIdenExtRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<EstIdenExtDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<EstIdenExtDto?>> ObtenerPorIdAsync(byte id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<EstIdenExtDto?>.Failure(result.Error!);
        return Result<EstIdenExtDto?>.Success(Mapper.Map<EstIdenExtDto>(result.Value));
    }

    public async Task<Result<IReadOnlyList<EstIdenExtDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<EstIdenExtDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<EstIdenExtDto>>.Success(Mapper.Map<IReadOnlyList<EstIdenExtDto>>(listResult.Value));
    }

    public async Task<Result<EstIdenExtDto?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorNombreAsync(nombre, ct);
        if (entityResult.IsFailure)
            return Result<EstIdenExtDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<EstIdenExtDto?>.Success(Mapper.Map<EstIdenExtDto?>(entity), allowNull: true);
    }
}
