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

public interface IUsuarioPermisoService : IServiceAsync<UsuarioPermiso, UsuarioPermisoDto>
{
    Task<Result<IReadOnlyList<UsuarioPermisoDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<UsuarioPermisoDto>> ConcederPermisoAsync(CrearUsuarioPermisoDto dto, int idUsrEjecutor, CancellationToken ct = default);
    Task<Result> RevocarPermisoAsync(int id, CancellationToken ct = default);
}

public class UsuarioPermisoService : ServiceAsync<UsuarioPermiso, UsuarioPermisoDto>, IUsuarioPermisoService
{
    private readonly IUsuarioPermisoRepository _repo;

    public UsuarioPermisoService(IUsuarioPermisoRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    public async Task<Result<IReadOnlyList<UsuarioPermisoDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<UsuarioPermisoDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<UsuarioPermisoDto>>.Success(Mapper.Map<IReadOnlyList<UsuarioPermisoDto>>(result.Value));
    }

    public async Task<Result<UsuarioPermisoDto>> ConcederPermisoAsync(CrearUsuarioPermisoDto dto, int idUsrEjecutor, CancellationToken ct = default)
    {
        var existente = await _repo.ObtenerPorUsuarioPermisoAsync(dto.IdUsuario, dto.IdPermiso, ct);
        if (existente.IsFailure) return Result<UsuarioPermisoDto>.Failure(existente.Error!);

        if (existente.Value != null)
        {
            if (existente.Value.Activo)
                return Result<UsuarioPermisoDto>.Failure("YA_ASIGNADO", "El permiso ya está asignado al usuario");

            existente.Value.Activo = true;
            existente.Value.IdTipoAsig = dto.IdTipoAsig;
            existente.Value.IdUsrMod = idUsrEjecutor;
            existente.Value.FecMod = DateTime.Now;
            var updResult = Repository.Update(existente.Value);
            if (updResult.IsFailure) return Result<UsuarioPermisoDto>.Failure(updResult.Error!);
            return Result<UsuarioPermisoDto>.Success(Mapper.Map<UsuarioPermisoDto>(existente.Value));
        }

        var entity = Mapper.Map<UsuarioPermiso>(dto);
        entity.Activo = true;
        entity.IdUsrCrea = idUsrEjecutor;
        entity.IdUsrMod = idUsrEjecutor;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<UsuarioPermisoDto>.Failure(addResult.Error!);
        return Result<UsuarioPermisoDto>.Success(Mapper.Map<UsuarioPermisoDto>(entity));
    }

    public async Task<Result> RevocarPermisoAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);

        result.Value.Activo = false;
        result.Value.FecMod = DateTime.Now;
        var updResult = Repository.Update(result.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }
}
