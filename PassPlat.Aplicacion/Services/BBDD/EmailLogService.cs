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
    Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerRecientesAsync(int? alcanceTenant, string? estado, int? idUsuario, int top, CancellationToken ct = default);
}

public class EmailLogService : ServiceAsync<EmailLog, EmailLogDto>, IEmailLogService
{
    private readonly IEmailLogRepository _repo;

    public EmailLogService(IEmailLogRepository repo, IMapper mapper)
        : base(repo, mapper) { _repo = repo; }

    // S109 · D6 — historial general: tope 1..500, acotado al tenant de la sesión y con los mismos filtros
    public async Task<Result<IReadOnlyList<EmailLogDto>>> ObtenerRecientesAsync(int? alcanceTenant, string? estado, int? idUsuario, int top, CancellationToken ct = default)
    {
        var limite = top <= 0 ? 1 : top > 500 ? 500 : top;
        var result = await _repo.ObtenerRecientesAsync(alcanceTenant, estado, idUsuario, limite, ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailLogDto>>.Failure(result.Error!);
        var lista = result.Value!.Where(el => alcanceTenant is null || el.IdTenant == alcanceTenant).ToList();
        return Result<IReadOnlyList<EmailLogDto>>.Success(Mapper.Map<IReadOnlyList<EmailLogDto>>(lista));
    }
}
