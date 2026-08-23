using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipAsigPermisoService : IServiceAsync<TipAsigPermiso, TipAsigPermisoDto>
{
    Task<Result<IReadOnlyList<TipAsigPermisoDto>>> ObtenerTodosAsync(CancellationToken ct = default);
}

public class TipAsigPermisoService : ServiceAsync<TipAsigPermiso, TipAsigPermisoDto>, ITipAsigPermisoService
{
    private readonly ITipAsigPermisoRepository _repo;

    public TipAsigPermisoService(ITipAsigPermisoRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    public async Task<Result<IReadOnlyList<TipAsigPermisoDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<TipAsigPermisoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<TipAsigPermisoDto>>.Success(Mapper.Map<IReadOnlyList<TipAsigPermisoDto>>(result.Value));
    }
}
