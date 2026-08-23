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

public interface ITenantEmailAccountService : IServiceAsync<TenantEmailAccount, TenantEmailAccountDto>
{
    Task<Result<IReadOnlyList<TenantEmailAccountDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<TenantEmailAccountDto>> CrearAsync(CrearTenantEmailAccountDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
}

public class TenantEmailAccountService : ServiceAsync<TenantEmailAccount, TenantEmailAccountDto>, ITenantEmailAccountService
{
    private readonly TenantEmailAccountRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public TenantEmailAccountService(TenantEmailAccountRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<TenantEmailAccountDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<TenantEmailAccountDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<TenantEmailAccountDto>>.Success(Mapper.Map<IReadOnlyList<TenantEmailAccountDto>>(listResult.Value));
    }

    public async Task<Result<TenantEmailAccountDto>> CrearAsync(CrearTenantEmailAccountDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<TenantEmailAccount>(dto);
        var addResult = _repo.Add(entity);
        if (addResult.IsFailure)
            return Result<TenantEmailAccountDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<TenantEmailAccountDto>.Success(Mapper.Map<TenantEmailAccountDto>(entity));
    }

    public async Task<Result> EliminarAsync(int id, CancellationToken ct = default)
    {
        var entityResult = await _repo.GetByIdAsync(id, ct);
        if (entityResult.IsFailure)
            return Result.Failure(entityResult.Error!);
        var removeResult = _repo.Remove(entityResult.Value);
        if (removeResult.IsFailure)
            return removeResult;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
