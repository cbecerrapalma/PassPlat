using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using CBP.Security.Cryptography.Services;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Datos;

namespace PassPlat.Aplicacion.Services;

public interface IEmailAccountService : IServiceAsync<EmailAccount, EmailAccountDto>
{
    Task<Result<IReadOnlyList<EmailAccountDto>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<EmailAccountDto?>> ObtenerPredeterminadaAsync(CancellationToken ct = default);
    Task<Result<EmailAccountDto>> CrearAsync(CrearEmailAccountDto dto, CancellationToken ct = default);
    Task<Result<EmailAccountDto>> ActualizarAsync(int id, ActualizarEmailAccountDto dto, CancellationToken ct = default);
}

public class EmailAccountService : ServiceAsync<EmailAccount, EmailAccountDto>, IEmailAccountService
{
    private readonly EmailAccountRepository _repo;
    private readonly IEncryptionService _encryption;
    private readonly IUnitOfWorkAsync _uow;

    public EmailAccountService(EmailAccountRepository repo, IEncryptionService encryption, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _encryption = encryption; _uow = uow; }

    public async Task<Result<IReadOnlyList<EmailAccountDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var listResult = await _repo.ObtenerActivosAsync(ct);
        if (listResult.IsFailure)
            return Result<IReadOnlyList<EmailAccountDto>>.Failure(listResult.Error!);
        return Result<IReadOnlyList<EmailAccountDto>>.Success(Mapper.Map<IReadOnlyList<EmailAccountDto>>(listResult.Value));
    }

    public async Task<Result<EmailAccountDto?>> ObtenerPredeterminadaAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPredeterminadaAsync(ct);
        if (result.IsFailure)
            return Result<EmailAccountDto?>.Failure(result.Error!);
        return Result<EmailAccountDto?>.Success(Mapper.Map<EmailAccountDto?>(result.Value), allowNull: true);
    }

    public async Task<Result<EmailAccountDto>> CrearAsync(CrearEmailAccountDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<EmailAccount>(dto);
        entity.Password = _encryption.Encrypt(dto.Password, "EmailAccount");
        var addResult = _repo.Add(entity);
        if (addResult.IsFailure)
            return Result<EmailAccountDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailAccountDto>.Success(Mapper.Map<EmailAccountDto>(entity));
    }

    public async Task<Result<EmailAccountDto>> ActualizarAsync(int id, ActualizarEmailAccountDto dto, CancellationToken ct = default)
    {
        var entityResult = await _repo.GetByIdAsync(id, ct);
        if (entityResult.IsFailure)
            return Result<EmailAccountDto>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        Mapper.Map(dto, entity);
        if (!string.IsNullOrEmpty(dto.Password))
            entity.Password = _encryption.Encrypt(dto.Password, "EmailAccount");
        var updateResult = _repo.Update(entity);
        if (updateResult.IsFailure)
            return Result<EmailAccountDto>.Failure(updateResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailAccountDto>.Success(Mapper.Map<EmailAccountDto>(entity));
    }
}
