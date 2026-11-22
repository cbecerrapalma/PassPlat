using AutoMapper;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IIntentoAccesoService : IServiceAsync<IntentoAcceso, IntentoAccesoDto>
{
    Task<Result<IReadOnlyList<IntentoAccesoDto>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
}

public class IntentoAccesoService : ServiceAsync<IntentoAcceso, IntentoAccesoDto>, IIntentoAccesoService
{
    private readonly IntentoAccesoRepository _repo;
    private readonly ITenantContext _tenantContext;

    public IntentoAccesoService(IntentoAccesoRepository repo, IMapper mapper, ITenantContext tenantContext)
        : base(repo, mapper)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    public override async Task<Result<IPagedResult<IntentoAccesoDto>>> GetPagedAsync(
        PaginationOptions<IntentoAcceso> options, CancellationToken ct = default)
    {
        var pagedResult = await _repo.GetPagedWithIncludesAsync(
            options.PageNumber, options.PageSize, _tenantContext.CurrentId, options.IncludeTotalCount, ct);
        if (pagedResult.IsFailure) return Result<IPagedResult<IntentoAccesoDto>>.Failure(pagedResult.Error!);
        var (items, totalCount) = pagedResult.Value;
        var dtos = Mapper.Map<IReadOnlyList<IntentoAccesoDto>>(items);
        var result = new PagedResultDto<IntentoAccesoDto>(dtos, totalCount, options.PageNumber, options.PageSize);
        return Result<IPagedResult<IntentoAccesoDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<IntentoAccesoDto>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        var intentosResult = await _repo.ObtenerIntentosRecientesAsync(idUsuario, minutos, ct);
        if (intentosResult.IsFailure) return Result<IReadOnlyList<IntentoAccesoDto>>.Failure(intentosResult.Error!);
        var intentos = intentosResult.Value;
        return Result<IReadOnlyList<IntentoAccesoDto>>.Success(Mapper.Map<IReadOnlyList<IntentoAccesoDto>>(intentos));
    }

    public async Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        var result = await _repo.ContarIntentosFallidosRecientesAsync(idUsuario, minutos, ct);
        if (result.IsFailure) return Result<int>.Failure(result.Error!);
        return Result<int>.Success(result.Value);
    }
}
