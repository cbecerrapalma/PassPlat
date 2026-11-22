using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

// S91 · D2 — alcanceTenant: null = sin restricción (is_system); con valor = solo ese tenant, y NotFound fuera de él.
public interface IGrupoService : IServiceAsync<Grupo, GrupoDto>
{
    Task<Result<IReadOnlyList<GrupoDto>>> ObtenerTodosAsync(int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GrupoDto>>> ObtenerPorTenantAsync(int idTenant, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<GrupoDto>> CrearAsync(CrearGrupoDto dto, CancellationToken ct = default);
    Task<Result<GrupoDto>> ActualizarAsync(int id, ActualizarGrupoDto dto, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, int? alcanceTenant, CancellationToken ct = default);
}

public class GrupoService : ServiceAsync<Grupo, GrupoDto>, IGrupoService
{
    private readonly IGrupoRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public GrupoService(IGrupoRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    private static bool FueraDeAlcance(int idTenant, int? alcanceTenant)
        => alcanceTenant.HasValue && alcanceTenant.Value != idTenant;

    public async Task<Result<IReadOnlyList<GrupoDto>>> ObtenerTodosAsync(int? alcanceTenant, CancellationToken ct = default)
    {
        var result = alcanceTenant.HasValue
            ? await _repo.ObtenerPorTenantAsync(alcanceTenant.Value, ct)
            : await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoDto>>.Success(Mapper.Map<IReadOnlyList<GrupoDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<GrupoDto>>> ObtenerPorTenantAsync(int idTenant, int? alcanceTenant, CancellationToken ct = default)
    {
        if (FueraDeAlcance(idTenant, alcanceTenant))
            return Result<IReadOnlyList<GrupoDto>>.Failure(CommonErrors.NotFound.Resource("Tenant"));

        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoDto>>.Success(Mapper.Map<IReadOnlyList<GrupoDto>>(result.Value));
    }

    public async Task<Result<GrupoDto>> CrearAsync(CrearGrupoDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<Grupo>(dto);
        entity.Activo = true;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<GrupoDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<GrupoDto>.Success(Mapper.Map<GrupoDto>(entity));
    }

    public async Task<Result<GrupoDto>> ActualizarAsync(int id, ActualizarGrupoDto dto, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result<GrupoDto>.Failure(result.Error!);
        if (FueraDeAlcance(result.Value.IdTenant, alcanceTenant))
            return Result<GrupoDto>.Failure(CommonErrors.NotFound.Entity("Grupo", id));

        var entity = result.Value;
        entity.Nombre = dto.Nombre;
        entity.Descripcion = dto.Descripcion;
        entity.Activo = dto.Activo;
        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result<GrupoDto>.Failure(updResult.Error!);
        return Result<GrupoDto>.Success(Mapper.Map<GrupoDto>(entity));
    }

    public async Task<Result> EliminarAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);
        if (FueraDeAlcance(result.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("Grupo", id));

        result.Value.Activo = false;
        var updResult = Repository.Update(result.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
