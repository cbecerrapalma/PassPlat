using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoCambioPwdService : IServiceAsync<TipoCambioPwd, TipoCambioPwdDto>
{
    Task<Result<IReadOnlyList<TipoCambioPwdDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TipoCambioPwdDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class TipoCambioPwdService : ServiceAsync<TipoCambioPwd, TipoCambioPwdDto>, ITipoCambioPwdService
{
    public TipoCambioPwdService(TipoCambioPwdRepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<TipoCambioPwdDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<TipoCambioPwdDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TipoCambioPwdDto?>.Failure(result.Error!);
        return Result<TipoCambioPwdDto?>.Success(Mapper.Map<TipoCambioPwdDto>(result.Value));
    }
}
