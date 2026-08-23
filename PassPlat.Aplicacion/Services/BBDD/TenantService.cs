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

public interface ITenantService : IServiceAsync<Tenant, TenantDto>
{
    Task<Result<IReadOnlyList<TenantDto>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<TenantDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<TenantDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TenantDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<int>> CountActivosAsync(CancellationToken ct = default);
    Task<Result<TenantDto>> CrearAsync(CrearTenantDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<TenantDto>> ActualizarAsync(int id, ActualizarTenantDto dto, CancellationToken ct = default);
    Task<Result<IPagedResult<TenantDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Tenant> options, string search, CancellationToken ct = default);
}

public class TenantService : ServiceAsync<Tenant, TenantDto>, ITenantService
{
    private readonly TenantRepository _repo;
    private readonly IConfigAppRepository _configAppRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ILogger<TenantService> _logger;

    public TenantService(TenantRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IConfigAppRepository configAppRepo, IEmailQueue emailQueue, ILogger<TenantService> logger)
        : base(repo, mapper)
    {
        _repo = repo;
        _configAppRepo = configAppRepo;
        _emailQueue = emailQueue;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<TenantDto>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await WhereAsync(t => t.Activo, ct);
    }

    public async Task<Result<TenantDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<TenantDto?>.Failure(result.Error!);
        return Result<TenantDto?>.Success(Mapper.Map<TenantDto>(result.Value));
    }

    public async Task<Result<TenantDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure) return Result<TenantDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<TenantDto>(entityResult.Value) : null;
        return Result<TenantDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<TenantDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure) return Result<IReadOnlyList<TenantDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<TenantDto>>.Success(Mapper.Map<IReadOnlyList<TenantDto>>(listResult.Value));
    }

    public async Task<Result<int>> CountActivosAsync(CancellationToken ct = default)
    {
        return await _repo.CountActivosAsync(ct);
    }

    public async Task<Result<TenantDto>> CrearAsync(CrearTenantDto dto, CancellationToken ct = default)
    {
        var existe = await _repo.ObtenerPorCodigoAsync(dto.Codigo, ct);
        if (existe.IsSuccess && existe.Value != null)
            return Result<TenantDto>.Failure("CODIGO_DUPLICADO", $"Ya existe un tenant con el código '{dto.Codigo}'");
        var entity = Tenant.Crear(dto.Codigo, dto.Nombre);
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<TenantDto>.Failure(addResult.Error!);

        await _uow.SaveChangesAsync(ct);
        await NotificarTenantAsync(EmailJobKind.TenantCreated, dto.Codigo, dto.Nombre, entity.Id, ct);
        return Result<TenantDto>.Success(Mapper.Map<TenantDto>(entity));
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure("NO_ENCONTRADO", "Tenant no encontrado");
        if (r.Value.EsSistema)
            return Result.Failure("TENANT_SISTEMA", "No se puede desactivar el tenant del sistema");
        r.Value.Desactivar();
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);

        await NotificarTenantAsync(EmailJobKind.TenantSuspended, r.Value.Codigo, r.Value.Nombre, r.Value.Id, ct);
        return Result.Success();
    }

    public async Task<Result<TenantDto>> ActualizarAsync(int id, ActualizarTenantDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result<TenantDto>.Failure(r.Error!);
        var entity = r.Value;
        var estabaInactivo = !entity.Activo;
        entity.Nombre = dto.Nombre;
        if (dto.Activo.HasValue)
            entity.Activo = dto.Activo.Value;
        var updResult = Repository.Update(entity);
        if (updResult.IsFailure) return Result<TenantDto>.Failure(updResult.Error!);
        await _uow.SaveChangesAsync(ct);
        if (estabaInactivo && entity.Activo)
            await NotificarTenantAsync(EmailJobKind.TenantReactivated, entity.Codigo, entity.Nombre, entity.Id, ct);
        return Result<TenantDto>.Success(Mapper.Map<TenantDto>(entity));
    }

    public async Task<Result<IPagedResult<TenantDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<Tenant> options, string search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoAsync(search, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<TenantDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<TenantDto>>(items);
        return Result<IPagedResult<TenantDto>>.Success(
            new PagedResultDto<TenantDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }

    private async Task NotificarTenantAsync(EmailJobKind kind, string codigo, string nombre, int idTenant, CancellationToken ct)
    {
        try
        {
            var templateCode = kind switch
            {
                EmailJobKind.TenantCreated => "tenant-created",
                EmailJobKind.TenantSuspended => "tenant-suspended",
                EmailJobKind.TenantReactivated => "tenant-reactivated",
                _ => "tenant-created"
            };

            var emailResult = await _configAppRepo.ObtenerPorGrupoAsync("General", ct);
            var configs = emailResult.IsSuccess ? emailResult.Value : null;
            var adminEmail = configs?.FirstOrDefault(c => c.Clave == "AdminEmail" && c.Activo)?.Valor;
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                _logger.LogInformation("Evento de tenant {Kind}: {Codigo} - {Nombre} (sin email admin configurado)", kind, codigo, nombre);
                return;
            }

            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                adminEmail,
                "Administrador",
                new Dictionary<string, object?> { ["TenantCodigo"] = codigo, ["TenantNombre"] = nombre },
                IdTenant: idTenant), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de tenant {Kind} para {Codigo}", kind, codigo);
        }
    }
}
