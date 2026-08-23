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

public interface IAppEmailAccountService : IServiceAsync<AppEmailAccount, AppEmailAccountDto>
{
    Task<Result<IReadOnlyList<AppEmailAccountDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default);
    Task<Result<AppEmailAccountDto>> CrearAsync(CrearAppEmailAccountDto dto, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, CancellationToken ct = default);
}

public class AppEmailAccountService : ServiceAsync<AppEmailAccount, AppEmailAccountDto>, IAppEmailAccountService
{
    private readonly AppEmailAccountRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public AppEmailAccountService(AppEmailAccountRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<IReadOnlyList<AppEmailAccountDto>>> ObtenerPorAppAsync(int idApp, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPorAppAsync(idApp, ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<AppEmailAccountDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<AppEmailAccountDto>>.Success(Mapper.Map<IReadOnlyList<AppEmailAccountDto>>(listResult.Value));
    }

    public async Task<Result<AppEmailAccountDto>> CrearAsync(CrearAppEmailAccountDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<AppEmailAccount>(dto);
        var addResult = _repo.Add(entity);
        if (addResult.IsFailure)
            return Result<AppEmailAccountDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<AppEmailAccountDto>.Success(Mapper.Map<AppEmailAccountDto>(entity));
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
