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

public interface IGrupoUsuarioService : IServiceAsync<GrupoUsuario, GrupoUsuarioDto>
{
    Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorGrupoAsync(int idGrupo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<GrupoUsuarioDto>> AgregarMiembroAsync(CrearGrupoUsuarioDto dto, int? idUsrMod = null, CancellationToken ct = default);
    Task<Result> RemoverMiembroAsync(int id, CancellationToken ct = default);
}

public class GrupoUsuarioService : ServiceAsync<GrupoUsuario, GrupoUsuarioDto>, IGrupoUsuarioService
{
    private readonly IGrupoUsuarioRepository _repo;

    public GrupoUsuarioService(IGrupoUsuarioRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    public async Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorGrupoAsync(int idGrupo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorGrupoAsync(idGrupo, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoUsuarioDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoUsuarioDto>>.Success(Mapper.Map<IReadOnlyList<GrupoUsuarioDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoUsuarioDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoUsuarioDto>>.Success(Mapper.Map<IReadOnlyList<GrupoUsuarioDto>>(result.Value));
    }

    public async Task<Result<GrupoUsuarioDto>> AgregarMiembroAsync(CrearGrupoUsuarioDto dto, int? idUsrMod = null, CancellationToken ct = default)
    {
        var entity = Mapper.Map<GrupoUsuario>(dto);
        entity.IdUsrMod = idUsrMod;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<GrupoUsuarioDto>.Failure(addResult.Error!);
        return Result<GrupoUsuarioDto>.Success(Mapper.Map<GrupoUsuarioDto>(entity));
    }

    public async Task<Result> RemoverMiembroAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);

        Repository.Remove(result.Value);
        return Result.Success();
    }
}
