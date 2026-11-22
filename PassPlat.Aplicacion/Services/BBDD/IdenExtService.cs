using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Data.Utilities.Helpers;
using CBP.Results;
using CBP.Results.Errors;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Enums;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Datos.Models;

namespace PassPlat.Aplicacion.Services;

public interface IIdenExtervice : IServiceAsync<IdenExt, IdenExtDto>
{
    // S99 · D1 — alcanceTenant: null = sin restricción (is_system); con valor = solo ese tenant, y NotFound fuera de él (S91/S95).
    Task<Result<IdenExtDto?>> ObtenerPorSubExternoAsync(int idProvIden, string subExterno, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorEstadoAsync(byte idEstado, int? alcanceTenant, CancellationToken ct = default);
    Task<Result<IPagedResult<IdenExtDto>>> BuscarAsync(BuscarIdenExtRequest request, CancellationToken ct = default);
    Task<Result<IdenExtDto>> CrearAsync(CrearIdenExtDto dto, CancellationToken ct = default);
    Task<Result> DesvincularAsync(long idIdentidad, int idUsuarioElimina, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> RevocarAsync(long idIdentidad, int idUsuarioRevoca, string? motivo, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> CambiarPrincipalAsync(long idIdentidad, int idUsuarioActor, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> CambiarEstadoAsync(long idIdentidad, byte idEstado, int? alcanceTenant, CancellationToken ct = default);
    Task<Result> ForzarMFAAsync(int idUsuario, int idUsuarioAdmin, int? alcanceTenant, CancellationToken ct = default);
}

public class IdenExtervice : ServiceAsync<IdenExt, IdenExtDto>, IIdenExtervice
{
    private readonly IdenExtRepository _repo;
    private readonly IExternalAuthRepository _externalAuthRepo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IMFAService _mfaService;
    private readonly IEmailQueue _emailQueue;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IHistorialIdenExtService _historialService;
    private readonly ILogger<IdenExtervice> _logger;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly IValidator<CrearIdenExtDto>? _crearValidator;

    // S123 · D7 — el validador es opcional al final: la DI lo resuelve (AddValidatorsFromAssembly) y las pruebas que no lo pasan no cambian
    public IdenExtervice(IdenExtRepository repo, IExternalAuthRepository externalAuthRepo, IUnitOfWorkAsync uow, IMapper mapper, IMFAService mfaService, IEmailQueue emailQueue, IUsuarioRepository usuarioRepo, IHistorialIdenExtService historialService, ILogger<IdenExtervice> logger, IUsuarioTenantRepository usuarioTenantRepo, IValidator<CrearIdenExtDto>? crearValidator = null)
        : base(repo, mapper)
    {
        _crearValidator = crearValidator;
        _usuarioTenantRepo = usuarioTenantRepo;
        _repo = repo;
        _externalAuthRepo = externalAuthRepo;
        _uow = uow;
        _mfaService = mfaService;
        _emailQueue = emailQueue;
        _usuarioRepo = usuarioRepo;
        _historialService = historialService;
        _logger = logger;
    }

    private static bool FueraDeAlcance(int idTenant, int? alcanceTenant)
        => alcanceTenant.HasValue && alcanceTenant.Value != idTenant;

    public async Task<Result<IdenExtDto?>> ObtenerPorSubExternoAsync(int idProvIden, string subExterno, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorSubExternoAsync(idProvIden, subExterno, ct);
        if (result.IsFailure) return Result<IdenExtDto?>.Failure(result.Error!);
        // S99 · D2 — fuera del alcance se trata como inexistente (FromResultQuery → 404)
        var dto = result.Value != null && !FueraDeAlcance(result.Value.IdTenant, alcanceTenant) ? Mapper.Map<IdenExtDto>(result.Value) : null;
        return Result<IdenExtDto?>.Success(dto, allowNull: true);
    }

    public async Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorUsuarioAsync(int idUsuario, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return Result<IReadOnlyList<IdenExtDto>>.Failure(result.Error!);
        var visibles = result.Value.Where(i => !FueraDeAlcance(i.IdTenant, alcanceTenant)).ToList();
        return Result<IReadOnlyList<IdenExtDto>>.Success(Mapper.Map<IReadOnlyList<IdenExtDto>>(visibles));
    }

    public async Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return Result<IReadOnlyList<IdenExtDto>>.Failure(result.Error!);
        return Result<IReadOnlyList<IdenExtDto>>.Success(Mapper.Map<IReadOnlyList<IdenExtDto>>(result.Value));
    }

    public async Task<Result<IReadOnlyList<IdenExtDto>>> ObtenerPorEstadoAsync(byte idEstado, int? alcanceTenant, CancellationToken ct = default)
    {
        var result = await _repo.ObtenerPorEstadoAsync(idEstado, ct);
        if (result.IsFailure) return Result<IReadOnlyList<IdenExtDto>>.Failure(result.Error!);
        var visibles = result.Value.Where(i => !FueraDeAlcance(i.IdTenant, alcanceTenant)).ToList();
        return Result<IReadOnlyList<IdenExtDto>>.Success(Mapper.Map<IReadOnlyList<IdenExtDto>>(visibles));
    }

    public async Task<Result<IPagedResult<IdenExtDto>>> BuscarAsync(BuscarIdenExtRequest request, CancellationToken ct = default)
    {
        var repoResult = await _repo.BuscarAsync(request, ct);
        if (repoResult.IsFailure) return Result<IPagedResult<IdenExtDto>>.Failure(repoResult.Error!);
        var items = Mapper.Map<IReadOnlyList<IdenExtDto>>(repoResult.Value.Items);
        var paged = new PagedResult<IdenExtDto>
        {
            Items = items,
            TotalCount = repoResult.Value.TotalCount,
            PageNumber = repoResult.Value.PageNumber,
            PageSize = repoResult.Value.PageSize
        };
        return Result<IPagedResult<IdenExtDto>>.Success(paged);
    }

    public async Task<Result<IdenExtDto>> CrearAsync(CrearIdenExtDto dto, CancellationToken ct = default)
    {
        // S123 · D7 — un cuerpo inválido responde 400 con el motivo (antes, 500 por la FK al guardar)
        if (_crearValidator is not null)
        {
            var validacion = await _crearValidator.ValidateAsync(dto, ct);
            if (!validacion.IsValid) return Result<IdenExtDto>.Failure(CreateValidationError(validacion));
        }

        var entity = IdenExt.Crear(dto.IdUsuario, dto.IdProvIden, dto.IdTenant, dto.SubExterno,
            dto.EmailExterno, dto.NombreExterno, dto.Avatar);
        entity.ProviderUserName = dto.ProviderUserName;

        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<IdenExtDto>.Failure(addResult.Error!);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning("Restricción de BD al vincular identidad externa en tenant {IdTenant} {Numero}: {Detalle}",
                dto.IdTenant, SqlErrorRedactor.GetErrorNumber(ex), SqlErrorRedactor.Redact(ex.InnerException?.Message));
            return Result<IdenExtDto>.Failure(new Error("DB_CONSTRAINT",
                "No se pudo vincular la identidad: el usuario o el proveedor no existen, o esa identidad ya está vinculada.", null, 400, ErrorType.Validation));
        }
        await NotificarVinculacionAsync(entity.Id, dto.IdUsuario, dto.IdProvIden, dto.IdTenant, EmailJobKind.ExternalIdentityLinked, ct);
        return Result<IdenExtDto>.Success(Mapper.Map<IdenExtDto>(entity));
    }

    public async Task<Result> DesvincularAsync(long idIdentidad, int idUsuarioElimina, int? alcanceTenant, CancellationToken ct = default)
    {
        var identidad = await _repo.GetByIdAsync(idIdentidad, ct);
        if (identidad.IsFailure) return Result.Failure(identidad.Error!);
        if (FueraDeAlcance(identidad.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("IdenExt", idIdentidad));

        var idUsuario = identidad.Value.IdUsuario;
        var idProvIden = identidad.Value.IdProvIden;
        var idTenant = identidad.Value.IdTenant;

        var result = await _externalAuthRepo.DesvincularIdentidadAsync(idIdentidad, idUsuarioElimina, revocarSesiones: true, ct);
        if (result.IsSuccess)
        {
            // S100 · D2 — el SP no pasa por EF: el historial necesita su propio commit
            try
            {
                await _historialService.RegistrarCambioAsync(idTenant, idUsuario, idIdentidad, idProvIden, "DESVINCULAR", "Activo", "Eliminada", idUsuarioElimina, false, null, ct);
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al registrar historial DESVINCULAR para identidad {Id}", idIdentidad); }

            await NotificarVinculacionAsync(idIdentidad, idUsuario, idProvIden, idTenant, EmailJobKind.ExternalIdentityUnlinked, ct);
        }
        return result;
    }

    public async Task<Result> RevocarAsync(long idIdentidad, int idUsuarioRevoca, string? motivo, int? alcanceTenant, CancellationToken ct = default)
    {
        var identidad = await _repo.GetByIdAsync(idIdentidad, ct);
        if (identidad.IsFailure) return Result.Failure(identidad.Error!);
        if (FueraDeAlcance(identidad.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("IdenExt", idIdentidad));

        var entity = identidad.Value;
        var estadoAnterior = entity.IdEstado.ToString();
        entity.IdEstado = (byte)EEstIdenExt.Revocado;
        entity.FecRevocacion = DateTime.Now;
        entity.IdUsuarioRevoca = idUsuarioRevoca;
        entity.MotivoRevocacion = motivo;

        var updateResult = _repo.Update(entity);
        if (updateResult.IsFailure) return Result.Failure(updateResult.Error!);

        // S100 · D2 — el historial entra en el mismo commit que el cambio
        try { await _historialService.RegistrarCambioAsync(entity.IdTenant, entity.IdUsuario, idIdentidad, entity.IdProvIden, "REVOCAR", estadoAnterior, ((byte)EEstIdenExt.Revocado).ToString(), idUsuarioRevoca, false, null, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al registrar historial REVOCAR para identidad {Id}", idIdentidad); }

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> CambiarPrincipalAsync(long idIdentidad, int idUsuarioActor, int? alcanceTenant, CancellationToken ct = default)
    {
        var identidad = await _repo.GetByIdAsync(idIdentidad, ct);
        if (identidad.IsFailure) return Result.Failure(identidad.Error!);
        if (FueraDeAlcance(identidad.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("IdenExt", idIdentidad));
        // S98 · D2 — el dueño sale de la identidad; el actor (historial), del token
        var idUsuario = identidad.Value.IdUsuario;

        var usuarioResult = await _usuarioRepo.GetByIdAsync(idUsuario, ct);
        if (usuarioResult.IsFailure) return Result.Failure(usuarioResult.Error!);

        var todasResult = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (todasResult.IsSuccess)
        {
            foreach (var id in todasResult.Value.Where(i => i.Id != idIdentidad && i.EsPrincipal))
            {
                id.EsPrincipal = false;
                _repo.Update(id);
            }
        }

        var anterior = identidad.Value.EsPrincipal.ToString();
        identidad.Value.EsPrincipal = true;
        _repo.Update(identidad.Value);

        // S100 · D2 — el historial entra en el mismo commit que el cambio
        try { await _historialService.RegistrarCambioAsync(identidad.Value.IdTenant, idUsuario, idIdentidad, identidad.Value.IdProvIden, "CAMBIAR_PRINCIPAL", anterior, "true", idUsuarioActor, false, null, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al registrar historial CAMBIAR_PRINCIPAL para identidad {Id}", idIdentidad); }

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> CambiarEstadoAsync(long idIdentidad, byte idEstado, int? alcanceTenant, CancellationToken ct = default)
    {
        var identidad = await _repo.GetByIdAsync(idIdentidad, ct);
        if (identidad.IsFailure) return Result.Failure(identidad.Error!);
        if (FueraDeAlcance(identidad.Value.IdTenant, alcanceTenant))
            return Result.Failure(CommonErrors.NotFound.Entity("IdenExt", idIdentidad));

        var anterior = identidad.Value.IdEstado.ToString();
        identidad.Value.IdEstado = idEstado;
        // S123 · D6 — reactivar quita la revocación; si no, SP_Auth_LoginExterno la seguiría rechazando
        if (idEstado == (byte)EEstIdenExt.Activo)
        {
            identidad.Value.FecRevocacion = null;
            identidad.Value.IdUsuarioRevoca = null;
            identidad.Value.MotivoRevocacion = null;
        }
        var updateResult = _repo.Update(identidad.Value);
        if (updateResult.IsFailure) return Result.Failure(updateResult.Error!);

        // S100 · D2 — el historial entra en el mismo commit que el cambio
        try { await _historialService.RegistrarCambioAsync(identidad.Value.IdTenant, identidad.Value.IdUsuario, idIdentidad, identidad.Value.IdProvIden, "CAMBIAR_ESTADO", anterior, idEstado.ToString(), null, false, null, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al registrar historial CAMBIAR_ESTADO para identidad {Id}", idIdentidad); }

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ForzarMFAAsync(int idUsuario, int idUsuarioAdmin, int? alcanceTenant, CancellationToken ct = default)
    {
        // S99 · D3 — un no-sistema solo fuerza MFA a miembros activos de su tenant (mismo criterio que UsuariosController)
        if (alcanceTenant is int propio)
        {
            var membresia = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, propio, ct);
            if (membresia.IsFailure) return Result.Failure(membresia.Error!);
            if (membresia.Value is null) return Result.Failure(CommonErrors.NotFound.Entity("Usuario", idUsuario));
        }

        var metodosResult = await _mfaService.ObtenerMetodosPorUsuarioAsync(idUsuario, ct);
        if (metodosResult.IsFailure) return Result.Failure(metodosResult.Error!);

        foreach (var metodo in metodosResult.Value)
        {
            var revokeResult = await _mfaService.RevocarMetodoAsync(idUsuario, metodo.Id, ct: ct);
            if (revokeResult.IsFailure)
                _logger.LogWarning("Error al revocar MFA {Id} para usuario {Usuario}: {Error}", metodo.Id, idUsuario, revokeResult.Error?.Message);
        }

        var userResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
        if (userResult.IsSuccess && userResult.Value?.Email is not null)
        {
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.MfaDisabled,
                userResult.Value.Email,
                userResult.Value.NomUsuario,
                new Dictionary<string, object?> { ["TipoAccion"] = "forzado" },
                IdTenant: userResult.Value.IdTenant,
                IdUsuario: idUsuario), ct);
        }

        // S100 · D1 — una fila FORZAR_MFA por identidad externa del usuario dentro del alcance (FK válidas); sin identidades, nada que registrar
        try
        {
            var identidades = await _repo.ObtenerPorUsuarioAsync(idUsuario, ct);
            if (identidades.IsSuccess)
            {
                var enAlcance = identidades.Value.Where(i => !FueraDeAlcance(i.IdTenant, alcanceTenant)).ToList();
                foreach (var identidad in enAlcance)
                    await _historialService.RegistrarCambioAsync(identidad.IdTenant, idUsuario, identidad.Id, identidad.IdProvIden, "FORZAR_MFA", null, "MFA revocado por administrador", idUsuarioAdmin, false, null, ct);
                if (enAlcance.Count > 0)
                    await _uow.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al registrar historial FORZAR_MFA para usuario {Id}", idUsuario); }

        return Result.Success();
    }

    private async Task NotificarVinculacionAsync(long idIdentidad, int idUsuario, int idProvIden, int idTenant, EmailJobKind kind, CancellationToken ct)
    {
        try
        {
            var userResult = await _usuarioRepo.GetByIdAsync(idUsuario, ct);
            if (userResult.IsFailure || userResult.Value == null) return;
            var user = userResult.Value;
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                user.Email,
                user.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["IdIdentidad"] = idIdentidad,
                    ["IdProvIden"] = idProvIden
                },
                IdTenant: idTenant,
                IdUsuario: idUsuario), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al encolar notificación de {Kind} para identidad {IdIdentidad}", kind, idIdentidad);
        }
    }
}
