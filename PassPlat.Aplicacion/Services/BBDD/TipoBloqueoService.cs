using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoBloqueoService : IServiceAsync<TipoBloqueo, TipoBloqueoDto>
{
    Task<Result<IReadOnlyList<TipoBloqueoDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TipoBloqueoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class TipoBloqueoService : ServiceAsync<TipoBloqueo, TipoBloqueoDto>, ITipoBloqueoService
{
    public TipoBloqueoService(TipoBloqueoRepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<TipoBloqueoDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<TipoBloqueoDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TipoBloqueoDto?>.Failure(result.Error!);
        return Result<TipoBloqueoDto?>.Success(Mapper.Map<TipoBloqueoDto>(result.Value));
    }
}
