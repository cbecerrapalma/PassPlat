using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IAppModuloService : IServiceAsync<AppModulo, AppModuloDto>
{
    Task<Result<IReadOnlyList<AppModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AppModuloDto>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<AppModuloDto>> AsignarModuloAsync(CrearAppModuloDto dto, CancellationToken ct = default);
    Task<Result> DesasignarModuloAsync(int id, CancellationToken ct = default);
}

public class AppModuloService : ServiceAsync<AppModulo, AppModuloDto>, IAppModuloService
{
    private readonly IAppModuloRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public AppModuloService(IAppModuloRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<IReadOnlyList<AppModuloDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerPorAppAsync(idApp, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<AppModuloDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<AppModuloDto>>.Success(Mapper.Map<IReadOnlyList<AppModuloDto>>(repoResult.Value));
    }

    public async Task<Result<IReadOnlyList<AppModuloDto>>> ObtenerActivosPorAppAsync(int idApp, CancellationToken ct = default)
    {
        var repoResult = await _repo.ObtenerActivosPorAppAsync(idApp, ct);
        if (repoResult.IsFailure) return Result<IReadOnlyList<AppModuloDto>>.Failure(repoResult.Error!);
        return Result<IReadOnlyList<AppModuloDto>>.Success(Mapper.Map<IReadOnlyList<AppModuloDto>>(repoResult.Value));
    }

    public async Task<Result<AppModuloDto>> AsignarModuloAsync(CrearAppModuloDto dto, CancellationToken ct = default)
    {
        var entity = AppModulo.Crear(dto.IdApp, dto.IdModulo);
        var result = _repo.Add(entity);
        if (result.IsFailure) return Result<AppModuloDto>.Failure(result.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<AppModuloDto>.Success(Mapper.Map<AppModuloDto>(entity));
    }

    public async Task<Result> DesasignarModuloAsync(int id, CancellationToken ct = default)
    {
        var entityResult = await _repo.GetByIdAsync(id, ct);
        if (entityResult.IsFailure) return Result.Failure(entityResult.Error!);
        entityResult.Value.Desactivar();
        _repo.Update(entityResult.Value);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
