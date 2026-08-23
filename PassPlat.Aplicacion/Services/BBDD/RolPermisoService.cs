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

public interface IRolPermisoService : IServiceAsync<RolPermiso, RolPermisoDto>
{
    Task<Result<IReadOnlyList<RolPermisoDto>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default);
    Task<Result<RolPermisoDto>> AsignarPermisoAsync(AsignarPermisoDto dto, CancellationToken ct = default);
    Task<Result> DesasignarPermisoAsync(int idRol, int idPermiso, CancellationToken ct = default);
}

public class RolPermisoService : ServiceAsync<RolPermiso, RolPermisoDto>, IRolPermisoService
{
    private readonly IRolPermisoRepository _repo;

    public RolPermisoService(IRolPermisoRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    public async Task<Result<IReadOnlyList<RolPermisoDto>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPermisosPorRolAsync(idRol, ct);
        if (listResult.IsFailure) return Result<IReadOnlyList<RolPermisoDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<RolPermisoDto>>.Success(Mapper.Map<IReadOnlyList<RolPermisoDto>>(listResult.Value));
    }

    public async Task<Result<RolPermisoDto>> AsignarPermisoAsync(AsignarPermisoDto dto, CancellationToken ct = default)
    {
        var existenteResult = await _repo.ObtenerActivoPorRolPermisoAsync(dto.IdRol, dto.IdPermiso, ct);
        if (existenteResult.IsFailure) return Result<RolPermisoDto>.Failure(existenteResult.Error!);
        var existente = existenteResult.Value;
        if (existente != null)
            return Result<RolPermisoDto>.Failure("YA_ASIGNADO", "El permiso ya está asignado a este rol");

        var entity = Mapper.Map<RolPermiso>(dto);
        entity.Activo = true;
        entity.FecCrea = DateTime.Now;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<RolPermisoDto>.Failure(addResult.Error!);
        return Result<RolPermisoDto>.Success(Mapper.Map<RolPermisoDto>(entity));
    }

    public async Task<Result> DesasignarPermisoAsync(int idRol, int idPermiso, CancellationToken ct = default)
    {
        var existenteResult = await _repo.ObtenerActivoPorRolPermisoAsync(idRol, idPermiso, ct);
        if (existenteResult.IsFailure) return Result.Failure(existenteResult.Error!);
        var existente = existenteResult.Value;
        if (existente == null)
            return Result.Failure("NO_ASIGNADO", "El permiso no está asignado a este rol");

        existente.Desactivar();
        var updResult = Repository.Update(existente);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
