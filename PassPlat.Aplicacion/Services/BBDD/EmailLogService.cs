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

public interface IEmailLogService : IServiceAsync<EmailLog, EmailLogDto>
{
    Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerPendientesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
}

public class EmailLogService : ServiceAsync<EmailLog, EmailLogDto>, IEmailLogService
{
    private readonly IEmailLogRepository _repo;

    public EmailLogService(IEmailLogRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    public async Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerPendientesAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPendientesAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailLogDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<EmailLogDto>>.Success(Mapper.Map<IReadOnlyList<EmailLogDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailLogDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<EmailLogDto>>.Success(Mapper.Map<IReadOnlyList<EmailLogDto>>(result.Value));
    }
}
