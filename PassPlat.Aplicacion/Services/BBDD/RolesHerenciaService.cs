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

public interface IRolesHerenciaService : IServiceAsync<RolesHerencia, RolesHerenciaDto>
{
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerHijosAsync(int idRolPadre, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPadresAsync(int idRolHijo, CancellationToken ct = default);
    Task<Result<RolesHerenciaDto>> CrearAsync(CrearRolesHerenciaDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
}

public class RolesHerenciaService : ServiceAsync<RolesHerencia, RolesHerenciaDto>, IRolesHerenciaService
{
    private readonly IRolesHerenciaRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public RolesHerenciaService(IRolesHerenciaRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolesHerenciaDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolesHerenciaDto>>.Success(Mapper.Map<IReadOnlyList<RolesHerenciaDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerHijosAsync(int idRolPadre, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerHijosAsync(idRolPadre, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolesHerenciaDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolesHerenciaDto>>.Success(Mapper.Map<IReadOnlyList<RolesHerenciaDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolesHerenciaDto>>> ObtenerPadresAsync(int idRolHijo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPadresAsync(idRolHijo, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolesHerenciaDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolesHerenciaDto>>.Success(Mapper.Map<IReadOnlyList<RolesHerenciaDto>>(result.Value));
    }

    public async Task<Result<RolesHerenciaDto>> CrearAsync(CrearRolesHerenciaDto dto, CancellationToken ct = default)
    {
        var existente = await _repo.ObtenerRelacionAsync(dto.IdRolHijo, dto.IdRolPadre, ct);
        if (existente.IsFailure) return Result<RolesHerenciaDto>.Failure(existente.Error!);
        if (existente.Value != null)
            return Result<RolesHerenciaDto>.Failure("YA_EXISTE", "La relación de herencia ya existe");

        var entity = Mapper.Map<RolesHerencia>(dto);
        entity.Activo = true;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<RolesHerenciaDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<RolesHerenciaDto>.Success(Mapper.Map<RolesHerenciaDto>(entity));
    }

    public async Task<Result> EliminarAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);

        var removeResult = Repository.Remove(result.Value);
        if (removeResult.IsFailure) return Result.Failure(removeResult.Error!);
        return Result.Success();
    }
}
