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

public interface IPermisoService : IServiceAsync<Permiso, PermisoDto>
{
    Task<Result<IReadOnlyList<PermisoDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<PermisoDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<PermisoDto>>> ObtenerPorModuloAsync(string modulo, CancellationToken ct = default);
    Task<Result<PermisoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<PermisoDto>> CrearAsync(CrearPermisoDto dto, CancellationToken ct = default);
    Task<Result<PermisoDto>> ActualizarAsync(int id, ActualizarPermisoDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
}

public class PermisoService : ServiceAsync<Permiso, PermisoDto>, IPermisoService
{
    private readonly IPermisoRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public PermisoService(IPermisoRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<PermisoDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<PermisoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<PermisoDto>>.Success(Mapper.Map<IReadOnlyList<PermisoDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerActivosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<PermisoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<PermisoDto>>.Success(Mapper.Map<IReadOnlyList<PermisoDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> ObtenerPorModuloAsync(string modulo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorModuloAsync(modulo, ct);
        if (result.IsFailure) return Result<IReadOnlyList<PermisoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<PermisoDto>>.Success(Mapper.Map<IReadOnlyList<PermisoDto>>(result.Value));
    }

    public async Task<Result<PermisoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<PermisoDto?>.Failure(result.Error!);
        return Result<PermisoDto?>.Success(Mapper.Map<PermisoDto?>(result.Value), allowNull: true);
    }

    public async Task<Result<PermisoDto>> CrearAsync(CrearPermisoDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<Permiso>(dto);
        entity.Activo = true;
        entity.FecCrea = DateTime.Now;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<PermisoDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<PermisoDto>.Success(Mapper.Map<PermisoDto>(entity));
    }

    public async Task<Result<PermisoDto>> ActualizarAsync(int id, ActualizarPermisoDto dto, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<PermisoDto>.Failure(result.Error!);

        var entity = result.Value;
        entity.Codigo = dto.Codigo;
        entity.Nombre = dto.Nombre;
        entity.Descripcion = dto.Descripcion;
        entity.IdModulo = dto.IdModulo;
        entity.Orden = dto.Orden;
        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result<PermisoDto>.Failure(updResult.Error!);
        return Result<PermisoDto>.Success(Mapper.Map<PermisoDto>(entity));
    }

    public async Task<Result> EliminarAsync(int id, CancellationToken ct = default)
    {
        var result = await _repo.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result.Failure(result.Error!);

        var entity = result.Value;
        entity.Activo = false;
        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
