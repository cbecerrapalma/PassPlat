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

public interface IAudIdenExtService : IServiceAsync<AudIdenExt, AudIdenExtDto>
{
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorProveedorAsync(int idProvIden, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorMetodoAsync(string metodo, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorOrigenAsync(string origen, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtResumenDto>>> ObtenerResumenPorTenantAsync(int idTenant, int limite = 20, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerErroresAsync(int idTenant, int limite = 50, CancellationToken ct = default);
}

public class AudIdenExtService : ServiceAsync<AudIdenExt, AudIdenExtDto>, IAudIdenExtService
{
    private readonly IAudIdenExtRepository _repo;
    private readonly IMapper _mapper;
    private readonly IUnitOfWorkAsync _uow;

    public AudIdenExtService(IAudIdenExtRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper)
    {
        _repo = repo;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorProveedorAsync(int idProvIden, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorProveedorAsync(idProvIden, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorMetodoAsync(string metodo, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorMetodoAsync(metodo, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerPorOrigenAsync(string origen, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorOrigenAsync(origen, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtResumenDto>>> ObtenerResumenPorTenantAsync(int idTenant, int limite = 20, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtResumenDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtResumenDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtResumenDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<AudIdenExtDto>>> ObtenerErroresAsync(int idTenant, int limite = 50, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerErroresAsync(idTenant, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<AudIdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AudIdenExtDto>>.Success(_mapper.Map<IReadOnlyList<AudIdenExtDto>>(result.Value));
    }
}
