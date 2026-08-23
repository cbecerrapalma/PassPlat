using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoDispService : IServiceAsync<TipoDisp, TipoDispDto>
{
    Task<Result<IReadOnlyList<TipoDispDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TipoDispDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class TipoDispService : ServiceAsync<TipoDisp, TipoDispDto>, ITipoDispService
{
    public TipoDispService(TipoDispRepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<TipoDispDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<TipoDispDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TipoDispDto?>.Failure(result.Error!);
        return Result<TipoDispDto?>.Success(Mapper.Map<TipoDispDto>(result.Value));
    }
}
