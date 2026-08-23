using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using CBP.Security.Cryptography.Services;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services;

public interface IConfigAppService : IServiceAsync<ConfigApp, ConfigAppDto>
{
    Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<Result<ConfigAppDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerPorGrupoAsync(string grupo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<ConfigAppDto>> CrearAsync(CrearConfigAppDto dto, CancellationToken ct = default);
    Task<Result<ConfigAppDto>> ActualizarAsync(int id, ActualizarConfigAppDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
    Task<Result<ConfigAppDto>> SetValorAsync(string grupo, string clave, string valor, string tipo = "string", string? descripcion = null, CancellationToken ct = default);
    Task<Result<IPagedResult<ConfigAppDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<ConfigApp> options, string search, CancellationToken ct = default);
}

public class ConfigAppService : ServiceAsync<ConfigApp, ConfigAppDto>, IConfigAppService
{
    private readonly ConfigAppRepository _repo;
    private readonly IEncryptionService _encryption;
    private readonly PassPlat.Aplicacion.Services.Email.IPassPlatEmailService _emailService;
    private readonly IUnitOfWorkAsync _uow;

    public ConfigAppService(
        ConfigAppRepository repo,
        IMapper mapper,
        IEncryptionService encryption,
        PassPlat.Aplicacion.Services.Email.IPassPlatEmailService emailService,
        IUnitOfWorkAsync uow)
        : base(repo, mapper)
    {
        _repo = repo;
        _encryption = encryption;
        _emailService = emailService;
        _uow = uow;
    }

    public async Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerTodasAsync(CancellationToken ct = default)
    {
        return await GetAllAsync(ct: ct);
    }

    public async Task<Result<ConfigAppDto?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<ConfigAppDto?>.Success(null, allowNull: true);
        return Result<ConfigAppDto?>.Success(Mapper.Map<ConfigAppDto>(result.Value));
    }

    public async Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerPorGrupoAsync(string grupo, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorGrupoAsync(grupo, ct);
        if (result.IsFailure) return Result<IReadOnlyList<ConfigAppDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<ConfigAppDto>>.Success(Mapper.Map<IReadOnlyList<ConfigAppDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<ConfigAppDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<ConfigAppDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<ConfigAppDto>>.Success(Mapper.Map<IReadOnlyList<ConfigAppDto>>(result.Value));
    }

    public async Task<Result<ConfigAppDto>> CrearAsync(CrearConfigAppDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<ConfigApp>(dto);
        entity.Activo = true;
        if (dto.EsEncriptado && !string.IsNullOrEmpty(dto.Valor))
        {
            var contextKey = $"ConfigApp:{dto.Clave}";
            entity.Valor = _encryption.Encrypt(dto.Valor, contextKey);
        }
        Repository.Add(entity);
        await _uow.SaveChangesAsync(ct);
        await InvalidateEmailCacheIfRelevant(entity.Grupo, ct);
        return Result<ConfigAppDto>.Success(Mapper.Map<ConfigAppDto>(entity));
    }

    public async Task<Result<ConfigAppDto>> ActualizarAsync(int id, ActualizarConfigAppDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result<ConfigAppDto>.Failure("NO_ENCONTRADO", "Configuración no encontrada");
        var entity = r.Value;
        var esEncriptadoOriginal = entity.EsEncriptado;
        var valorOriginal = entity.Valor;
        Mapper.Map(dto, entity);

        var transitionResult = ApplyEncryptionTransition(entity, dto, esEncriptadoOriginal, valorOriginal);
        if (transitionResult.IsFailure)
            return Result<ConfigAppDto>.Failure(transitionResult.Error!);

        Repository.Update(entity);
        await InvalidateEmailCacheIfRelevant(entity.Grupo, ct);
        return Result<ConfigAppDto>.Success(Mapper.Map<ConfigAppDto>(entity));
    }

    private Result ApplyEncryptionTransition(ConfigApp entity, ActualizarConfigAppDto dto, bool esEncriptadoOriginal, string valorOriginal)
    {
        var contextKey = $"ConfigApp:{entity.Clave}";

        if (dto.EsEncriptado && !esEncriptadoOriginal)
        {
            if (!string.IsNullOrEmpty(dto.Valor))
                entity.Valor = _encryption.Encrypt(dto.Valor, contextKey);
            return Result.Success();
        }

        if (dto.EsEncriptado && esEncriptadoOriginal && dto.Valor != valorOriginal && !string.IsNullOrEmpty(dto.Valor))
        {
            entity.Valor = _encryption.Encrypt(dto.Valor, contextKey);
            return Result.Success();
        }

        if (!dto.EsEncriptado && esEncriptadoOriginal && dto.Valor == valorOriginal && !string.IsNullOrEmpty(valorOriginal))
        {
            try
            {
                entity.Valor = _encryption.Decrypt(valorOriginal, contextKey);
            }
            catch (Exception)
            {
                return Result.Failure("DECRYPTION_ERROR",
                    $"No se pudo descifrar el valor de '{entity.Clave}'. Re-ingrese el valor en texto plano para desactivar el cifrado.");
            }
        }
        return Result.Success();
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure("NO_ENCONTRADO", "Configuración no encontrada");
        r.Value.Activo = false;
        Repository.Update(r.Value);
        await InvalidateEmailCacheIfRelevant(r.Value.Grupo, ct);
        return Result.Success();
    }

    public async Task<Result<ConfigAppDto>> SetValorAsync(string grupo, string clave, string valor, string tipo = "string", string? descripcion = null, CancellationToken ct = default)
    {
        var result = await _repo.SetValorAsync(grupo, clave, valor, tipo, descripcion, null, ct);
        if (result.IsFailure) return Result<ConfigAppDto>.Failure(result.Error!);
        await InvalidateEmailCacheIfRelevant(grupo, ct);
        return Result<ConfigAppDto>.Success(Mapper.Map<ConfigAppDto>(result.Value));
    }

    private async Task InvalidateEmailCacheIfRelevant(string grupo, CancellationToken ct = default)
    {
        if (string.Equals(grupo, "Email", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(grupo, "Branding", StringComparison.OrdinalIgnoreCase))
        {
            await _repo.InvalidarCacheGrupoAsync(grupo, ct);
            await _emailService.InvalidateCacheAsync(ct);
        }
    }

    public async Task<Result<IPagedResult<ConfigAppDto>>> ObtenerPaginadoConBusquedaAsync(PaginationOptions<ConfigApp> options, string search, CancellationToken ct = default)
    {
        var pagResult = await _repo.BuscarPaginadoAsync(search, options.PageNumber, options.PageSize, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<ConfigAppDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<ConfigAppDto>>(items);
        return Result<IPagedResult<ConfigAppDto>>.Success(
            new PagedResultDto<ConfigAppDto>(mapped, totalCount, options.PageNumber, options.PageSize));
    }
}
