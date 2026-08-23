using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface ITipoAuditoriaService : IServiceAsync<TipoAuditoria, TipoAuditoriaDto>
{
    Task<Result<IReadOnlyList<TipoAuditoriaDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TipoAuditoriaDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class TipoAuditoriaService : ServiceAsync<TipoAuditoria, TipoAuditoriaDto>, ITipoAuditoriaService
{
    public TipoAuditoriaService(TipoAuditoriaRepository repo, IMapper mapper)
        : base(repo, mapper) { }

    public async Task<Result<IReadOnlyList<TipoAuditoriaDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<TipoAuditoriaDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TipoAuditoriaDto?>.Failure(result.Error!);
        return Result<TipoAuditoriaDto?>.Success(Mapper.Map<TipoAuditoriaDto>(result.Value));
    }
}
