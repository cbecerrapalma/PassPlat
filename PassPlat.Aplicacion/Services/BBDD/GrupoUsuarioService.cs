using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

// S91 · D3 — alcanceTenant como en IGrupoService: null = is_system; con valor = solo grupos de ese tenant.
public interface IGrupoUsuarioService : IServiceAsync<GrupoUsuario, GrupoUsuarioDto>
{
    Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorGrupoAsync(int idGrupo, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<GrupoUsuarioDto>> AgregarMiembroAsync(CrearGrupoUsuarioDto dto, int? alcanceTenant, int? idUsrMod = null, CancellationToken ct = default);
    Task<Result> RemoverMiembroAsync(int id, int? alcanceTenant, CancellationToken ct = default);
}

public class GrupoUsuarioService : ServiceAsync<GrupoUsuario, GrupoUsuarioDto>, IGrupoUsuarioService
{
    private readonly IGrupoUsuarioRepository _repo;
    private readonly IGrupoRepository _grupoRepo;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;

    public GrupoUsuarioService(IGrupoUsuarioRepository repo, IGrupoRepository grupoRepo, IUsuarioTenantRepository usuarioTenantRepo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _grupoRepo = grupoRepo; _usuarioTenantRepo = usuarioTenantRepo; }

    private async Task<Result<Grupo>> GrupoEnAlcanceAsync(int idGrupo, int? alcanceTenant, CancellationToken ct)
    {
        var grupo = await _grupoRepo.GetByIdAsync(idGrupo, ct);
        if (grupo.IsFailure) return grupo;
        if (alcanceTenant.HasValue && grupo.Value.IdTenant != alcanceTenant.Value)
            return Result<Grupo>.Failure(CommonErrors.NotFound.Entity("Grupo", idGrupo));
        return grupo;
    }

    public async Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorGrupoAsync(int idGrupo, int? alcanceTenant, CancellationToken ct = default)
    {
        if (alcanceTenant.HasValue)
        {
            var grupo = await GrupoEnAlcanceAsync(idGrupo, alcanceTenant, ct);
            if (grupo.IsFailure) return Result<IReadOnlyList<GrupoUsuarioDto>>.Failure(grupo.Error!);
        }

        var result = await _repo.ObtenerPorGrupoAsync(idGrupo, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoUsuarioDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<GrupoUsuarioDto>>.Success(Mapper.Map<IReadOnlyList<GrupoUsuarioDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<GrupoUsuarioDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<GrupoUsuarioDto>>.Failure(result.Error!);

        IReadOnlyList<GrupoUsuario> lista = result.Value;
        if (alcanceTenant.HasValue)
            lista = [.. lista.Where(gu => gu.Grupo?.IdTenant == alcanceTenant.Value)];
        return Result<IReadOnlyList<GrupoUsuarioDto>>.Success(Mapper.Map<IReadOnlyList<GrupoUsuarioDto>>(lista));
    }

    public async Task<Result<GrupoUsuarioDto>> AgregarMiembroAsync(CrearGrupoUsuarioDto dto, int? alcanceTenant, int? idUsrMod = null, CancellationToken ct = default)
    {
        var grupo = await GrupoEnAlcanceAsync(dto.IdGrupo, alcanceTenant, ct);
        if (grupo.IsFailure) return Result<GrupoUsuarioDto>.Failure(grupo.Error!);

        // El usuario debe ser miembro activo del tenant del grupo, también para is_system.
        var membresia = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(dto.IdUsuario, grupo.Value.IdTenant, ct);
        if (membresia.IsFailure) return Result<GrupoUsuarioDto>.Failure(membresia.Error!);
        if (membresia.Value is null)
            return Result<GrupoUsuarioDto>.Failure("SIN_MEMBRESIA_TENANT", "El usuario no pertenece al tenant del grupo.");

        var entity = Mapper.Map<GrupoUsuario>(dto);
        entity.IdUsrMod = idUsrMod;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<GrupoUsuarioDto>.Failure(addResult.Error!);
        return Result<GrupoUsuarioDto>.Success(Mapper.Map<GrupoUsuarioDto>(entity));
    }

    public async Task<Result> RemoverMiembroAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure) return Result.Failure(result.Error!);
        if (alcanceTenant.HasValue)
        {
            var grupo = await GrupoEnAlcanceAsync(result.Value.IdGrupo, alcanceTenant, ct);
            if (grupo.IsFailure) return Result.Failure(CommonErrors.NotFound.Entity("GrupoUsuario", id));
        }

        Repository.Remove(result.Value);
        return Result.Success();
    }
}
