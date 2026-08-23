using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
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
    Task<Result<ConfProvIdenDto>> ActualizarAsync(int id, ActualizarConfProvIdenDto dto, CancellationToken ct = default);
    Task<Result> DesactivarAsync(int id, CancellationToken ct = default);
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

    public ConfProvIdenService(ConfProvIdenRepository repo, IUnitOfWorkAsync uow, IMapper mapper, IEncryptionService encryption, IEmailQueue emailQueue, ILogger<ConfProvIdenService> logger, IAuditoriaPwdService auditoriaService, IHttpContextAccessor httpContextAccessor)
        : base(repo, mapper)
    {
        _repo = repo;
        _uow = uow;
        _encryption = encryption;
        _emailQueue = emailQueue;
        _logger = logger;
        _auditoriaService = auditoriaService;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<Result<ConfProvIdenDto>> CrearAsync(CrearConfProvIdenDto dto, CancellationToken ct = default)
    {
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
            dto.AuthorizationEndpoint, dto.TokenEndpoint, dto.JwksUri, dto.Issuer,
            dto.ResponseType, dto.GrantType, dto.ExtraParams);

        entity.ClientSecret = _encryption.Encrypt(dto.ClientSecret.Trim(), "ConfProvIden");

        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<ConfProvIdenDto>.Failure(addResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result<ConfProvIdenDto>.Success(Mapper.Map<ConfProvIdenDto>(entity));
    }

    public async Task<Result<ConfProvIdenDto>> ActualizarAsync(int id, ActualizarConfProvIdenDto dto, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result<ConfProvIdenDto>.Failure(r.Error!);

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

        await _uow.SaveChangesAsync(ct);
        return Result<ConfProvIdenDto>.Success(Mapper.Map<ConfProvIdenDto>(r.Value));
    }

    public async Task<Result> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var r = await Repository.GetByIdAsync(id, ct);
        if (r.IsFailure) return Result.Failure(r.Error!);

        r.Value.Activo = false;
        r.Value.Estado = 0;
        var updResult = Repository.Update(r.Value);
        if (updResult.IsFailure) return Result.Failure(updResult.Error!);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
