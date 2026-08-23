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

public interface IRolService : IServiceAsync<Rol, RolDto>
{
    Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolDto>>> ObtenerGlobalesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolDto>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<RolDto?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default);
    Task<Result<RolDto>> CrearAsync(CrearRolDto dto, int? idUsrEjecutor = null, CancellationToken ct = default);
    Task<Result<RolDto>> ActualizarAsync(int id, string nombre, string? descripcion, bool activo, int? idUsrEjecutor = null, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, int? idUsrEjecutor = null, CancellationToken ct = default);
    Task<Result<IPagedResult<RolDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Rol> options, string search, CancellationToken ct = default);
}

public class RolService : ServiceAsync<Rol, RolDto>, IRolService
{
    private readonly RolRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public RolService(RolRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerTodosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerGlobalesAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerGlobalesAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolDto>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerParaTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolDto>>.Success(Mapper.Map<IReadOnlyList<RolDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<RolLookupDto>>> ObtenerLookupPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerParaTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<RolLookupDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<RolLookupDto>>.Success(Mapper.Map<IReadOnlyList<RolLookupDto>>(result.Value));
    }

    public async Task<Result<RolDto?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorCodigoAsync(idTenant, codigo, ct);
        if (result.IsFailure) return Result<RolDto?>.Failure(result.Error!);
        return Result<RolDto?>.Success(Mapper.Map<RolDto?>(result.Value), allowNull: true);
    }

    public async Task<Result<RolDto>> CrearAsync(CrearRolDto dto, int? idUsrEjecutor = null, CancellationToken ct = default)
    {
        var idsPermisos = dto.IdsPermisos is { Count: > 0 }
            ? string.Join(",", dto.IdsPermisos)
            : null;

        var spResult = await _repo.CrearConSPAsync(
            dto.IdTenant, dto.Codigo, dto.Nombre, dto.Descripcion,
            dto.IdPolitica, idsPermisos, ct);

        if (spResult.IsFailure)
            return Result<RolDto>.Failure(spResult.Error!);

        if (spResult.Value.Resultado != 0)
            return Result<RolDto>.Failure($"SP_ERROR_{spResult.Value.Resultado}", spResult.Value.Mensaje ?? "Error al crear el rol");

        var entityResult = await Repository.GetByIdAsync(spResult.Value.Id!.Value, ct);
        if (entityResult.IsFailure)
            return Result<RolDto>.Failure(entityResult.Error!);

        return Result<RolDto>.Success(Mapper.Map<RolDto>(entityResult.Value));
    }

    public async Task<Result> DesactivarAsync(int id, int? idUsrEjecutor = null, CancellationToken ct = default)
    {
        var spResult = await _repo.DesactivarConSPAsync(id, idUsrEjecutor, ct);

        if (spResult.IsFailure)
            return Result.Failure(spResult.Error!);

        if (spResult.Value.Resultado != 0)
            return Result.Failure($"SP_ERROR_{spResult.Value.Resultado}", spResult.Value.Mensaje ?? "Error al desactivar el rol");

        return Result.Success();
    }

    public async Task<Result<RolDto>> ActualizarAsync(int id, string nombre, string? descripcion, bool activo, int? idUsrEjecutor = null, CancellationToken ct = default)
    {
        var spResult = await _repo.ActualizarConSPAsync(id, nombre, descripcion, activo, idUsrEjecutor, ct);

        if (spResult.IsFailure)
            return Result<RolDto>.Failure(spResult.Error!);

        if (spResult.Value.Resultado != 0)
            return Result<RolDto>.Failure($"SP_ERROR_{spResult.Value.Resultado}", spResult.Value.Mensaje ?? "Error al actualizar el rol");

        var entityResult = await Repository.GetByIdAsync(id, ct);
        if (entityResult.IsFailure)
            return Result<RolDto>.Failure(entityResult.Error!);

        return Result<RolDto>.Success(Mapper.Map<RolDto>(entityResult.Value));
    }

    public async Task<Result<IPagedResult<RolDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Rol> options, string search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoAsync(search, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<RolDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<RolDto>>(items);
        return Result<IPagedResult<RolDto>>.Success(
            new PagedResultDto<RolDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }
}
