using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IAppService : IServiceAsync<App, AppDto>
{
    Task<Result<IReadOnlyList<AppDto>>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<Result<AppDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<AppDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AppDto>>> ObtenerActivasAsync(CancellationToken ct = default);
    Task<Result<AppDto>> CrearAsync(CrearAppDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<IPagedResult<AppDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<App> options, string search, CancellationToken ct = default);
}

public class AppService : ServiceAsync<App, AppDto>, IAppService
{
    private readonly AppRepository _repo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IEmailQueue _emailQueue;
    private readonly IConfigAppRepository _configAppRepo;
    private readonly ILogger<AppService> _logger;

    public AppService(AppRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEmailQueue emailQueue, IConfigAppRepository configAppRepo, ILogger<AppService> logger)
        : base(repo, mapper) { _repo = repo; _uow = uow; _emailQueue = emailQueue; _configAppRepo = configAppRepo; _logger = logger; }

    public async Task<Result<IReadOnlyList<AppDto>>> ObtenerTodasAsync(CancellationToken ct = default)
    {
        return await WhereAsync(a => a.Activa, ct);
    }

    public async Task<Result<AppDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<AppDto?>.Failure(result.Error!);
        return Result<AppDto?>.Success(Mapper.Map<AppDto>(result.Value));
    }

    public async Task<Result<AppDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure) return Result<AppDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<AppDto>(entityResult.Value) : null;
        return Result<AppDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<AppDto>>> ObtenerActivasAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerActivasAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<AppDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<AppDto>>.Success(Mapper.Map<IReadOnlyList<AppDto>>(result.Value));
    }

    public async Task<Result<AppDto>> CrearAsync(CrearAppDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<App>(dto);
        entity.Activa = true;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<AppDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        await _repo.InvalidarCacheAsync(ct);
        await NotificarAppRegistradaAsync(entity.Codigo, entity.Nombre, entity.Id, ct);
        return Result<AppDto>.Success(Mapper.Map<AppDto>(entity));
    }

    private async Task NotificarAppRegistradaAsync(string codigo, string nombre, int idApp, CancellationToken ct)
    {
        try
        {
            var emailResult = await _configAppRepo.ObtenerPorGrupoAsync("General", ct);
            var configs = emailResult.IsSuccess ? emailResult.Value : null;
            var adminEmail = configs?.FirstOrDefault(c => c.Clave == "AdminEmail" && c.Activo)?.Valor;
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                _logger.LogInformation("App registrada: {Codigo} - {Nombre} (sin email admin configurado)", codigo, nombre);
                return;
            }
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.AppRegistered,
                adminEmail,
                "Administrador",
                new Dictionary<string, object?> { ["AppCodigo"] = codigo, ["AppNombre"] = nombre },
                IdTenant: null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de app registrada {Codigo}", codigo);
        }
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure("NO_ENCONTRADO", "App no encontrada");
        r.Value.Activa = false;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);
        await _repo.InvalidarCacheAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IPagedResult<AppDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<App> options, string search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoAsync(search, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<AppDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<AppDto>>(items);
        return Result<IPagedResult<AppDto>>.Success(
            new PagedResultDto<AppDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }
}
