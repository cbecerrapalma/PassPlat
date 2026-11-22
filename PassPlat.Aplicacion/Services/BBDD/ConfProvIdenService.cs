using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Data.Utilities.Helpers;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Security.Cryptography.Services;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Enums;
using System.Security.Claims;

namespace PassPlat.Aplicacion.Services;

public interface IConfProvIdenService : IServiceAsync<ConfProvIden, ConfProvIdenDto>
{
    Task<Result<ConfProvIdenDto?>> ObtenerConfiguracionAsync(int idTenant, int idProvIden, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfProvIdenDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<ConfProvIdenDto>> CrearAsync(CrearConfProvIdenDto dto, CancellationToken ct = default);
    // S99 · D1 — alcanceTenant: null = sin restricción (is_system); con valor = solo ese tenant, y NotFound fuera de él.
    Task<Result<ConfProvIdenDto>> ActualizarAsync(int id, ActualizarConfProvIdenDto dto, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, int? alcanceTenant, CancellationToken ct = default);
    // S122 · D1 — reactiva una configuración desactivada (Activo = true, Estado = 1), con el mismo alcance.
    Task<Result> ActivarAsync(int id, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IPagedResult<ConfProvIdenDto>>> BuscarPaginadoAsync(int? idTenant, string? search, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class ConfProvIdenService : ServiceAsync<ConfProvIden, ConfProvIdenDto>, IConfProvIdenService
{
    private readonly ConfProvIdenRepository _repo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IEncryptionService _encryption;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<ConfProvIdenService> _logger;
    private readonly IAuditoriaPwdService _auditoriaService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IExternalProviderValidator _validator;
    private readonly IValidator<CrearConfProvIdenDto>? _crearValidator;

    // S123 · D7 — el validador es opcional al final: la DI lo resuelve (AddValidatorsFromAssembly) y las pruebas que no lo pasan no cambian
    public ConfProvIdenService(ConfProvIdenRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEncryptionService encryption, IEmailQueue emailQueue, ILogger<ConfProvIdenService> logger, IAuditoriaPwdService auditoriaService, IHttpContextAccessor httpContextAccessor, IExternalProviderValidator validator, IValidator<CrearConfProvIdenDto>? crearValidator = null)
        : base(repo, mapper)
    {
        _crearValidator = crearValidator;
        _repo = repo;
        _uow = uow;
        _encryption = encryption;
        _emailQueue = emailQueue;
        _logger = logger;
        _auditoriaService = auditoriaService;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
    }

    public async Task<Result<ConfProvIdenDto?>> ObtenerConfiguracionAsync(int idTenant, int idProvIden, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerConfiguracionAsync(idTenant, idProvIden, ct);
        if (result.IsFailure) return Result<ConfProvIdenDto?>.Failure(result.Error!);
        var dto = result.Value != null ? Mapper.Map<ConfProvIdenDto>(result.Value) : null;
        return Result<ConfProvIdenDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<ConfProvIdenDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<ConfProvIdenDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<ConfProvIdenDto>>.Success(Mapper.Map<IReadOnlyList<ConfProvIdenDto>>(result.Value));
    }

    // S95 · D3 — lista paginada con búsqueda; página o tamaño inválidos → 1 y 20
    public async Task<Result<IPagedResult<ConfProvIdenDto>>> BuscarPaginadoAsync(int? idTenant, string? search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var pagina = pageNumber < 1 ? 1 : pageNumber;
        var tamano = pageSize < 1 ? 20 : pageSize;
        var pagResult = await _repo.BuscarPaginadoAsync(idTenant, search, pagina, tamano, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<ConfProvIdenDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<ConfProvIdenDto>>(items);
        // S122 · D3 — por qué el login no muestra cada fila (null = visible)
        for (var i = 0; i < mapped.Count && i < items.Count; i++)
            mapped[i].MotivoNoDisponible = _validator.Validar(items[i]).Motivo;
        return Result<IPagedResult<ConfProvIdenDto>>.Success(
            new PagedResultDto<ConfProvIdenDto>(mapped, totalCount, pagina, tamano));
    }

    public async Task<Result<ConfProvIdenDto>> CrearAsync(CrearConfProvIdenDto dto, CancellationToken ct = default)
    {
        // S123 · D7 — un cuerpo inválido responde 400 con el motivo (antes, 500 por la FK al guardar)
        if (_crearValidator is not null)
        {
            var validacion = await _crearValidator.ValidateAsync(dto, ct);
            if (!validacion.IsValid) return Result<ConfProvIdenDto>.Failure(CreateValidationError(validacion));
        }

        if (dto.RolDefecto.HasValue)
        {
            var rolResult = await _repo.ObtenerRolDefectoAsync(dto.RolDefecto.Value, ct);
            if (rolResult.IsFailure || rolResult.Value is null)
                return Result<ConfProvIdenDto>.Failure("ROL_NO_EXISTE", "El rol seleccionado no existe");
            if (!rolResult.Value.Activo)
                return Result<ConfProvIdenDto>.Failure("ROL_INACTIVO", "El rol seleccionado está inactivo");
            if (rolResult.Value.IdTenant.HasValue && rolResult.Value.IdTenant != dto.IdTenant)
                return Result<ConfProvIdenDto>.Failure("ROL_OTRO_TENANT", "El rol seleccionado pertenece a otro tenant");
        }

        var entity = ConfProvIden.Crear(
            dto.IdTenant, dto.IdProvIden, dto.ClientId, dto.ClientSecret, dto.Callback,
            dto.Scopes, dto.RedirectUri, dto.RolDefecto, dto.GuardarTokens,
            dto.PermitirAutoLink, dto.AutoProvisionar, dto.RequiereMFALocal, dto.Metadata,
            dto.PermitirLogin, dto.PermitirCrearUsuario, dto.PermitirVincular, dto.PermitirDesvincular,
            dto.PermitirPasswordLocal, dto.ObligaMFA, dto.PermitirCambioEmail, dto.PermitirCambioNombre,
            dto.PermitirSincronizarAvatar, dto.PermitirSincronizarPerfil, dto.FrecuenciaSincronizacion, dto.Prioridad, dto.OrdenVisual,
            dto.Logo, dto.Color, dto.Tooltip, dto.Descripcion,
            dto.AuthorizationEndpoint, dto.TokenEndpoint,
            dto.ResponseType, dto.GrantType, dto.ExtraParams);

        entity.ClientSecret = _encryption.Encrypt(dto.ClientSecret.Trim(), "ConfProvIden");
        // S126 · D4 — apps a las que sirve (vacía = todas)
        entity.AsignarApps(dto.IdsApps);

        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<ConfProvIdenDto>.Failure(addResult.Error!);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning("Restricción de BD al crear configuración de proveedor en tenant {IdTenant} {Numero}: {Detalle}",
                dto.IdTenant, SqlErrorRedactor.GetErrorNumber(ex), SqlErrorRedactor.Redact(ex.InnerException?.Message));
            return Result<ConfProvIdenDto>.Failure(new Error("DB_CONSTRAINT",
                "No se pudo guardar la configuración: el tenant o el proveedor no existen, o ese proveedor ya está configurado en el tenant.", null, 400, ErrorType.Validation));
        }
        return Result<ConfProvIdenDto>.Success(Mapper.Map<ConfProvIdenDto>(entity));
    }

    private static bool FueraDeAlcance(int idTenant, int? alcanceTenant)
        => alcanceTenant.HasValue && alcanceTenant.Value != idTenant;

    public async Task<Result<ConfProvIdenDto>> ActualizarAsync(int id, ActualizarConfProvIdenDto dto, int? alcanceTenant, CancellationToken ct = default)
    {
        // S126 · D4 — con sus enlaces a apps, para reemplazarlos
        var r = await _repo.ObtenerConAppsAsync(id, ct);
        if (r.IsFailure) return Result<ConfProvIdenDto>.Failure(r.Error!);
        if (FueraDeAlcance(r.Value.IdTenant, alcanceTenant))
            return Result<ConfProvIdenDto>.Failure(CommonErrors.NotFound.Entity("ConfProvIden", id));

        if (dto.RolDefecto.HasValue)
        {
            var rolResult = await _repo.ObtenerRolDefectoAsync(dto.RolDefecto.Value, ct);
            if (rolResult.IsFailure || rolResult.Value is null)
                return Result<ConfProvIdenDto>.Failure("ROL_NO_EXISTE", "El rol seleccionado no existe");
            if (!rolResult.Value.Activo)
                return Result<ConfProvIdenDto>.Failure("ROL_INACTIVO", "El rol seleccionado está inactivo");
            if (rolResult.Value.IdTenant.HasValue && rolResult.Value.IdTenant != r.Value.IdTenant)
                return Result<ConfProvIdenDto>.Failure("ROL_OTRO_TENANT", "El rol seleccionado pertenece a otro tenant");
        }

        var secretChanged = !string.IsNullOrWhiteSpace(dto.ClientSecret);
        Mapper.Map(dto, r.Value);
        // S122 · D2 — Estado y Activo son un solo concepto en la edición: 1 activa; 0 o 2 desactivan
        if (dto.Estado.HasValue) r.Value.Activo = dto.Estado.Value == 1;
        if (dto.IdsApps is not null) r.Value.AsignarApps(dto.IdsApps);
        if (secretChanged && dto.ClientSecret is { } clientSecret)
        {
            r.Value.ClientSecret = _encryption.Encrypt(clientSecret.Trim(), "ConfProvIden");

            var userIdStr = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out var usuarioId);
            if (usuarioId > 0)
            {
                await _auditoriaService.RegistrarAuditoriaAsync(new RegistrarAuditoriaPwdDto
                {
                    IdUsuario = usuarioId,
                    IdTipoAccion = (int)ETipoAuditoria.ConfiguracionOAuthCambio,
                    IdTenant = r.Value.IdTenant,
                    IdUsrEjecutor = usuarioId,
                    Detalles = $"OAuth Client Secret actualizado — ConfProvIden Id={id}, Tenant Id={r.Value.IdTenant}",
                    NivelRiesgo = 2
                }, ct);
            }
        }
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result<ConfProvIdenDto>.Failure(updResult.Error!);

        // S126 · D4 — una app inexistente responde 400 con el motivo (no 500)
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning("Restricción de BD al actualizar configuración de proveedor {Id} {Numero}: {Detalle}",
                id, SqlErrorRedactor.GetErrorNumber(ex), SqlErrorRedactor.Redact(ex.InnerException?.Message));
            return Result<ConfProvIdenDto>.Failure(new Error("DB_CONSTRAINT",
                "No se pudo guardar la configuración: alguna de las aplicaciones no existe.", null, 400, ErrorType.Validation));
        }
        return Result<ConfProvIdenDto>.Success(Mapper.Map<ConfProvIdenDto>(r.Value));
    }

    public async Task<Result> DesactivarAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);
        if (FueraDeAlcance(r.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("ConfProvIden", id));

        r.Value.Activo = false;
        r.Value.Estado = 0;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    // S122 · D1 — espejo de DesactivarAsync: fuera del alcance, NotFound sin cambios
    public async Task<Result> ActivarAsync(int id, int? alcanceTenant, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);
        if (FueraDeAlcance(r.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("ConfProvIden", id));

        r.Value.Activo = true;
        r.Value.Estado = 1;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
