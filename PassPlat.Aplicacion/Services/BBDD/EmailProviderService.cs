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
    Task<Result<EmailProviderDto>> CrearAsync(CrearEmailProviderDto dto, CancellationToken ct = default);
    Task<Result<EmailProviderDto>> ActualizarAsync(byte id, ActualizarEmailProviderDto dto, CancellationToken ct = default);
}

public class EmailProviderService : ServiceAsync<EmailProvider, EmailProviderDto>, IEmailProviderService
{
    private readonly EmailProviderRepository _repo;
    private readonly IUnitOfWorkAsync _uow;

    public EmailProviderService(EmailProviderRepository repo, IUnitOfWorkAsync uow, IMapper mapper)
        : base(repo, mapper) { _repo = repo; _uow = uow; }

    public async Task<Result<EmailProviderDto>> CrearAsync(CrearEmailProviderDto dto, CancellationToken ct = default)
    {
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var invalido = Validar(nombre, dto.Descripcion);
        if (codigo.Length == 0 || codigo.Length > 20)
            invalido ??= "El código es obligatorio y admite hasta 20 caracteres.";
        if (invalido is not null)
            return Result<EmailProviderDto>.Failure("DATOS_INVALIDOS", invalido);

        var existente = await _repo.ObtenerPorCodigoAsync(codigo, ct);
        if (existente.IsFailure)
            return Result<EmailProviderDto>.Failure(existente.Error!);
        if (existente.Value is not null)
            return Result<EmailProviderDto>.Failure("CODIGO_DUPLICADO", $"Ya existe un provider con el código {codigo}.");

        var entity = EmailProvider.Crear(codigo, nombre, Limpiar(dto.Descripcion));
        var addResult = _repo.Add(entity);
        if (addResult.IsFailure)
            return Result<EmailProviderDto>.Failure(addResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailProviderDto>.Success(Mapper.Map<EmailProviderDto>(entity));
    }

    public async Task<Result<EmailProviderDto>> ActualizarAsync(byte id, ActualizarEmailProviderDto dto, CancellationToken ct = default)
    {
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var invalido = Validar(nombre, dto.Descripcion);
        if (invalido is not null)
            return Result<EmailProviderDto>.Failure("DATOS_INVALIDOS", invalido);

        var entityResult = await _repo.GetByIdAsync(id, ct);
        if (entityResult.IsFailure)
            return Result<EmailProviderDto>.Failure(entityResult.Error!);
        var entity = entityResult.Value;
        entity.Actualizar(nombre, Limpiar(dto.Descripcion), dto.Activo);
        var updateResult = _repo.Update(entity);
        if (updateResult.IsFailure)
            return Result<EmailProviderDto>.Failure(updateResult.Error!);
        await _uow.SaveChangesAsync(ct);
        return Result<EmailProviderDto>.Success(Mapper.Map<EmailProviderDto>(entity));
    }

    private static string? Validar(string nombre, string? descripcion)
    {
        if (nombre.Length == 0 || nombre.Length > 100)
            return "El nombre es obligatorio y admite hasta 100 caracteres.";
        if (descripcion is not null && descripcion.Trim().Length > 200)
            return "La descripción admite hasta 200 caracteres.";
        return null;
    }

    private static string? Limpiar(string? descripcion) =>
        string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
}
