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

public interface IRolPoliticaPwdService : IServiceAsync<RolPoliticaPwd, RolPoliticaPwdDto>
{
    Task<Result<IReadOnlyList<RolPoliticaPwdDto>>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default);
    Task<Result<RolPoliticaPwdDto>> CrearAsync(CrearRolPoliticaPwdDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
}

public class RolPoliticaPwdService : ServiceAsync<RolPoliticaPwd, RolPoliticaPwdDto>, IRolPoliticaPwdService
{
    private readonly RolPoliticaPwdRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public RolPoliticaPwdService(RolPoliticaPwdRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<RolPoliticaPwdDto>>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPorRolAsync(idRol, ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<RolPoliticaPwdDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<RolPoliticaPwdDto>>.Success(Mapper.Map<IReadOnlyList<RolPoliticaPwdDto>>(listResult.Value));
    }

    public async Task<Result<RolPoliticaPwdDto>> CrearAsync(CrearRolPoliticaPwdDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<RolPoliticaPwd>(dto);
        entity.Activo = true;
        entity.FecCrea = DateTime.Now;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<RolPoliticaPwdDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<RolPoliticaPwdDto>.Success(Mapper.Map<RolPoliticaPwdDto>(entity));
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure("NO_ENCONTRADO", "Registro no encontrado");
        r.Value.Activo = false;
        r.Value.FecMod = DateTime.Now;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        return Result.Success();
    }

}
