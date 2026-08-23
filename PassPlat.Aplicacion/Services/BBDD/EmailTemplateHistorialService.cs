using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IEmailTemplateHistorialService : IServiceAsync<EmailTemplateHistorial, EmailTemplateHistorialDto>
{
    Task<Result<IReadOnlyList<EmailTemplateHistorialDto>>> ObtenerPorTemplateAsync(int idTemplate, CancellationToken ct = default);
}

public class EmailTemplateHistorialService : ServiceAsync<EmailTemplateHistorial, EmailTemplateHistorialDto>, IEmailTemplateHistorialService
{
    private readonly EmailTemplateHistorialRepository _repo;

    public EmailTemplateHistorialService(EmailTemplateHistorialRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<EmailTemplateHistorialDto>>> ObtenerPorTemplateAsync(int idTemplate, CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerPorTemplateAsync(idTemplate, ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<EmailTemplateHistorialDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<EmailTemplateHistorialDto>>.Success(Mapper.Map<IReadOnlyList<EmailTemplateHistorialDto>>(listResult.Value));
    }
}
