using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services.Email;

public interface IEmailTemplatePartialService : IServiceAsync<EmailTemplatePartial, EmailTemplatePartialDto>
{
    Task<Result<EmailTemplatePartialDto>> CrearAsync(CrearEmailTemplatePartialDto dto, CancellationToken ct = default);
    Task<Result<EmailTemplatePartialDto>> ActualizarAsync(ActualizarEmailTemplatePartialDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<EmailTemplatePartialDto?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplatePartialDto>>> ObtenerActivosAsync(CancellationToken ct = default);
}

public class EmailTemplatePartialService : ServiceAsync<EmailTemplatePartial, EmailTemplatePartialDto>, IEmailTemplatePartialService
{
    private readonly IEmailTemplatePartialRepository _repo;
    private readonly IEmailTemplateStoreService _store;
    private readonly IUnitOfWorkAsync _uow;

    public EmailTemplatePartialService(IEmailTemplatePartialRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEmailTemplateStoreService store)
        : base(repo, mapper)
    {
        _repo = repo;
        _store = store;
        _uow = uow;
    }

    public async Task<Result<EmailTemplatePartialDto>> CrearAsync(CrearEmailTemplatePartialDto dto, CancellationToken ct = default)
    {
        var entity = EmailTemplatePartial.Crear(dto.Nombre, dto.CuerpoHtml, dto.Descripcion);
        Repository.Add(entity);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailTemplatePartialDto>.Success(Mapper.Map<EmailTemplatePartialDto>(entity));
    }

    public async Task<Result<EmailTemplatePartialDto>> ActualizarAsync(ActualizarEmailTemplatePartialDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(dto.Id, ct);
        if (r.IsFailure) return Result<EmailTemplatePartialDto>.Failure(r.Error!);
        var entity = r.Value;

        if (dto.CuerpoHtml != null) entity.CuerpoHtml = dto.CuerpoHtml;
        if (dto.Descripcion != null) entity.Descripcion = dto.Descripcion;
        if (dto.Activo.HasValue) entity.Activo = dto.Activo.Value;
        entity.FecMod = DateTime.Now;

        Repository.Update(entity);
        await _store.InvalidateAllCacheAsync(ct);
        return Result<EmailTemplatePartialDto>.Success(Mapper.Map<EmailTemplatePartialDto>(entity));
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);
        r.Value.Activo = false;
        r.Value.FecMod = DateTime.Now;
        Repository.Update(r.Value);
        await _store.InvalidateAllCacheAsync(ct);
        return Result.Success();
    }

    public async Task<Result<EmailTemplatePartialDto?>> ObtenerPorNombreAsync(string nombre, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorNombreAsync(nombre, ct);
        if (entityResult.IsFailure) return Result<EmailTemplatePartialDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<EmailTemplatePartialDto>(entityResult.Value) : null;
        return Result<EmailTemplatePartialDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<EmailTemplatePartialDto>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var result = await _repo.ObtenerActivosAsync(ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailTemplatePartialDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<EmailTemplatePartialDto>>.Success(Mapper.Map<IReadOnlyList<EmailTemplatePartialDto>>(result.Value));
    }
}
