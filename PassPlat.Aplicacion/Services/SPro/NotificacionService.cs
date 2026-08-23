using AutoMapper;
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
    Task<Result<IReadOnlyList<NotificacionDto>>> ObtenerNoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<int>> ContarNoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Task<Result> MarcarComoLeidaAsync(int idNotificacion, CancellationToken ct = default);
    Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<NotificacionDto>> CrearNotificacionAsync(CrearNotificacionDto dto, CancellationToken ct = default);
}

public class NotificacionService : ServiceAsync<Notificacion, NotificacionDto>, INotificacionService
{
    private readonly NotificacionRepository _repo;

    public NotificacionService(NotificacionRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<NotificacionDto>>> ObtenerNoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        var notifResult = await _repo.ObtenerNoLeidasAsync(idUsuario, ct);
        if (notifResult.IsFailure) return Result<IReadOnlyList<NotificacionDto>>.Failure(notifResult.Error!);
        var notificaciones = notifResult.Value;
        return Result<IReadOnlyList<NotificacionDto>>.Success(Mapper.Map<IReadOnlyList<NotificacionDto>>(notificaciones));
    }

    public async Task<Result<int>> ContarNoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        var result = await _repo.ContarNoLeidasAsync(idUsuario, ct);
        if (result.IsFailure) return Result<int>.Failure(result.Error!);
        return Result<int>.Success(result.Value);
    }

    public async Task<Result> MarcarComoLeidaAsync(int idNotificacion, CancellationToken ct = default)
    {
        var repoResult = _repo.MarcarComoLeida(idNotificacion);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result> MarcarTodasComoLeidasAsync(int idUsuario, CancellationToken ct = default)
    {
        var repoResult = await _repo.MarcarTodasComoLeidasAsync(idUsuario, ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<NotificacionDto>> CrearNotificacionAsync(CrearNotificacionDto dto, CancellationToken ct = default)
    {
        var notifResult = _repo.CrearNotificacion(dto.IdUsuario, dto.IdTenant, dto.TipoNotif, dto.Asunto, dto.Mensaje);
        if (notifResult.IsFailure) return Result<NotificacionDto>.Failure(notifResult.Error!);
        return Result<NotificacionDto>.Success(Mapper.Map<NotificacionDto>(notifResult.Value));
    }
}
