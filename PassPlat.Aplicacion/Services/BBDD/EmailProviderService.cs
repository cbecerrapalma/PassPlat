using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IEmailProviderService : IServiceAsync<EmailProvider, EmailProviderDto>
{
    Task<Result<EmailProviderDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
}

public class EmailProviderService : ServiceAsync<EmailProvider, EmailProviderDto>, IEmailProviderService
{
    private readonly EmailProviderRepository _repo;

    public EmailProviderService(EmailProviderRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<EmailProviderDto?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (entityResult.IsFailure)
            return Result<EmailProviderDto?>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        return Result<EmailProviderDto?>.Success(Mapper.Map<EmailProviderDto?>(entity), allowNull: true);
    }
}
