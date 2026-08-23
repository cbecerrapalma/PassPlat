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

public interface IEmailTemplateService : IServiceAsync<EmailTemplate, EmailTemplateDto>
{
    Task<Result<EmailTemplateDto>> CrearAsync(CrearEmailTemplateDto dto, CancellationToken ct = default);
    Task<Result<EmailTemplateDto>> ActualizarAsync(ActualizarEmailTemplateDto dto, CancellationToken ct = default);
    Task<Result> PublicarAsync(PublicarTemplateDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<EmailTemplateDto?>> ObtenerPorNombreCulturaAsync(string nombre, string cultura, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplateDto>>> ObtenerPorCategoriaAsync(string categoria, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmailTemplateDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
}

public class EmailTemplateService : ServiceAsync<EmailTemplate, EmailTemplateDto>, IEmailTemplateService
{
    private readonly IEmailTemplateRepository _repo;
    private readonly IEmailTemplateStoreService _store;
    private readonly IUnitOfWorkAsync _uow;

    public EmailTemplateService(IEmailTemplateRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEmailTemplateStoreService store)
        : base(repo, mapper)
    {
        _repo = repo;
        _store = store;
        _uow = uow;
    }

    public async Task<Result<EmailTemplateDto>> CrearAsync(CrearEmailTemplateDto dto, CancellationToken ct = default)
    {
        var entity = EmailTemplate.Crear(
            dto.Nombre, dto.Asunto, dto.CuerpoHtml,
            dto.Cultura, dto.Descripcion, dto.Categoria, dto.IdTenant);
        Repository.Add(entity);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailTemplateDto>.Success(Mapper.Map<EmailTemplateDto>(entity));
    }

    public async Task<Result<EmailTemplateDto>> ActualizarAsync(ActualizarEmailTemplateDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(dto.Id, ct);
        if (r.IsFailure) return Result<EmailTemplateDto>.Failure(r.Error!);
        var entity = r.Value;

        if (dto.Asunto != null) entity.Asunto = dto.Asunto;
        if (dto.CuerpoHtml != null) entity.CuerpoHtml = dto.CuerpoHtml;
        if (dto.CuerpoTexto != null) entity.CuerpoTexto = dto.CuerpoTexto;
        if (dto.Descripcion != null) entity.Descripcion = dto.Descripcion;
        if (dto.Categoria != null) entity.Categoria = dto.Categoria;
        if (dto.Estado != null) entity.Estado = dto.Estado;
        if (dto.VariablesDoc != null) entity.VariablesDoc = dto.VariablesDoc;
        entity.FecMod = DateTime.Now;

        Repository.Update(entity);
        await _store.InvalidateAllCacheAsync(ct);
        return Result<EmailTemplateDto>.Success(Mapper.Map<EmailTemplateDto>(entity));
    }

    public async Task<Result> PublicarAsync(PublicarTemplateDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(dto.Id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);
        r.Value.Publicar(idUsrPublico: 0);
        Repository.Update(r.Value);
        await _store.InvalidateCacheAsync(r.Value.Nombre, r.Value.Cultura, r.Value.IdTenant, ct);
        return Result.Success();
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);
        r.Value.Desactivar(idUsrMod: 0);
        Repository.Update(r.Value);
        await _store.InvalidateCacheAsync(r.Value.Nombre, r.Value.Cultura, r.Value.IdTenant, ct);
        return Result.Success();
    }

    public async Task<Result<EmailTemplateDto?>> ObtenerPorNombreCulturaAsync(string nombre, string cultura, int? idTenant, CancellationToken ct = default)
    {
        var entityResult = await _repo.ObtenerPorNombreCulturaAsync(nombre, cultura, idTenant, ct);
        if (entityResult.IsFailure) return Result<EmailTemplateDto?>.Failure(entityResult.Error!);
        var dto = entityResult.Value != null ? Mapper.Map<EmailTemplateDto>(entityResult.Value) : null;
        return Result<EmailTemplateDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<EmailTemplateDto>>> ObtenerPorCategoriaAsync(string categoria, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorCategoriaAsync(categoria, ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailTemplateDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<EmailTemplateDto>>.Success(Mapper.Map<IReadOnlyList<EmailTemplateDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<EmailTemplateDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<EmailTemplateDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<EmailTemplateDto>>.Success(Mapper.Map<IReadOnlyList<EmailTemplateDto>>(result.Value));
    }
}
