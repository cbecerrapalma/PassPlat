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

public interface INotificacionService : IServiceAsync<Notificacion, NotificacionDto>
{
    // S118 · D5 — alcanceTenant: null = sin acotar (sistema o el propio usuario); con valor = solo ese tenant.
    Task<Result<IReadOnlyList<NotificacionDto>>> ObtenerNoLeidasAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> MarcarComoLeidaAsync(int idNotificacion, CancellationToken ct = default);
    Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IPagedResult<NotificacionDto>>> ObtenerPaginadoPorTenantAsync(int idTenant, PaginationOptions<Notificacion> options, CancellationToken ct = default);
    Task<Result<NotificacionDto>> CrearNotificacionAsync(CrearNotificacionDto dto, CancellationToken ct = default);
}

public class NotificacionService : ServiceAsync<Notificacion, NotificacionDto>, INotificacionService
{
    private readonly NotificacionRepository _repo;

    public NotificacionService(NotificacionRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<NotificacionDto>>> ObtenerNoLeidasAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var notifResult = await _repo.ObtenerNoLeidasAsync(idUsuario, alcanceTenant, ct);
        if (notifResult.IsFailure) return Result<IReadOnlyList<NotificacionDto>>.Failure(notifResult.Error!);
        var notificaciones = notifResult.Value;
        return Result<IReadOnlyList<NotificacionDto>>.Success(Mapper.Map<IReadOnlyList<NotificacionDto>>(notificaciones));
    }

    public async Task<Result> MarcarComoLeidaAsync(int idNotificacion, CancellationToken ct = default)
    {
        var repoResult = _repo.MarcarComoLeida(idNotificacion);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var repoResult = await _repo.MarcarTodasComoLeidasAsync(idUsuario, alcanceTenant, ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<IPagedResult<NotificacionDto>>> ObtenerPaginadoPorTenantAsync(int idTenant, PaginationOptions<Notificacion> options, CancellationToken ct = default)
    {
        var pagResult = await _repo.ObtenerPaginadoPorTenantAsync(idTenant, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<NotificacionDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<NotificacionDto>>(items);
        return Result<IPagedResult<NotificacionDto>>.Success(
            new PagedResultDto<NotificacionDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }

    public async Task<Result<NotificacionDto>> CrearNotificacionAsync(CrearNotificacionDto dto, CancellationToken ct = default)
    {
        var notifResult = _repo.CrearNotificacion(dto.IdUsuario, dto.IdTenant, dto.TipoNotif, dto.Asunto, dto.Mensaje);
        if (notifResult.IsFailure) return Result<NotificacionDto>.Failure(notifResult.Error!);
        return Result<NotificacionDto>.Success(Mapper.Map<NotificacionDto>(notifResult.Value));
    }
}
