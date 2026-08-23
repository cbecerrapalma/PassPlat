using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IEstadoUsrService : IServiceAsync<EstadoUsr, EstadoUsrDto>
{
    Task<Result<IReadOnlyList<EstadoUsrDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<EstadoUsrDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EstadoUsrDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<EstadoUsrDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class EstadoUsrService : ServiceAsync<EstadoUsr, EstadoUsrDto>, IEstadoUsrService
{
    private readonly EstadoUsrRepository _repo;

    public EstadoUsrService(EstadoUsrRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<EstadoUsrDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<EstadoUsrDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<EstadoUsrDto?>.Failure(result.Error!);
        return Result<EstadoUsrDto?>.Success(Mapper.Map<EstadoUsrDto>(result.Value));
    }

    public async Task<Result<IReadOnlyList<EstadoUsrDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<EstadoUsrDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<EstadoUsrDto>>.Success(Mapper.Map<IReadOnlyList<EstadoUsrDto>>(listResult.Value));
    }

    public async Task<Result<EstadoUsrDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure)
            return Result<EstadoUsrDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<EstadoUsrDto?>.Success(Mapper.Map<EstadoUsrDto?>(entity), allowNull: true);
    }
}
