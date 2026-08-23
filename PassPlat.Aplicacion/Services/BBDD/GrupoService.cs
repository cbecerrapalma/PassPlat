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

public interface IGrupoService : IServiceAsync<Grupo, GrupoDto>
{
    Task<Result<IReadOnlyList<GrupoDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<GrupoDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<GrupoDto?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default);
    Task<Result<GrupoDto>> CrearAsync(CrearGrupoDto dto, CancellationToken ct = default);
    Task<Result<GrupoDto>> ActualizarAsync(int id, ActualizarGrupoDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
}

public class GrupoService : ServiceAsync<Grupo, GrupoDto>, IGrupoService
{
    private readonly IGrupoRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public GrupoService(IGrupoRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<GrupoDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoDto>>.Success(Mapper.Map<IReadOnlyList<GrupoDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<GrupoDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoDto>>.Success(Mapper.Map<IReadOnlyList<GrupoDto>>(result.Value));
    }

    public async Task<Result<GrupoDto?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorCodigoAsync(idTenant, codigo, ct);
        if (result.IsFailure) return Result<GrupoDto?>.Failure(result.Error!);
        return Result<GrupoDto?>.Success(Mapper.Map<GrupoDto?>(result.Value), allowNull: true);
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

    public async Task<Result<GrupoDto>> ActualizarAsync(int id, ActualizarGrupoDto dto, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result<GrupoDto>.Failure(result.Error!);

        var entity = result.Value;
        entity.Nombre = dto.Nombre;
        entity.Descripcion = dto.Descripcion;
        entity.Activo = dto.Activo;
        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result<GrupoDto>.Failure(updResult.Error!);
        return Result<GrupoDto>.Success(Mapper.Map<GrupoDto>(entity));
    }

    public async Task<Result> EliminarAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);

        result.Value.Activo = false;
        var updResult = Repository.Update(result.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
