using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Results.Errors;
using CBP.Results;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Enums;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "MFA_VER")]
[EnableRateLimiting("MFAPolicy")]
public class MfaController : BaseApiController
{
    private readonly IMFAService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly ILogger<MfaController> _logger;
    private readonly IVisibilidadUsuarioService _visibilidad;

    public MfaController(IMFAService service, ITenantContext tenantContext, IUnitOfWorkAsync uow, IUsuarioRepository usuarioRepo, IUsuarioTenantRepository usuarioTenantRepo, ILogger<MfaController> logger, IVisibilidadUsuarioService visibilidad)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
        _usuarioRepo = usuarioRepo;
        _usuarioTenantRepo = usuarioTenantRepo;
        _logger = logger;
        _visibilidad = visibilidad;
    }

    // S65.1: el texto de la excepción no se devuelve al cliente; queda en el log con el traceId.
    private IActionResult ErrorAlGuardarMfa(string accion, Exception ex)
    {
        _logger.LogError(ex, "Error al guardar MFA {Accion} trace {TraceId}", accion, HttpContext.TraceIdentifier);
        return Conflict(new { codigo = "ERROR_AL_GUARDAR_MFA", mensaje = "No se pudo guardar el método MFA. Intente nuevamente." });
    }

    [AllowAnonymous]
    [HttpPost("validar")]
    public async Task<IActionResult> Validar([FromBody] ValidarMfaRequest request, CancellationToken ct)
    {
        if (request.IdTenant <= 0 || request.IdUsuario <= 0)
            return BadRequest(new { codigo = "PARAMETROS_INVALIDOS", mensaje = "IdUsuario e IdTenant son obligatorios" });

        var usuarioResult = await _usuarioRepo.GetByIdAsync(request.IdUsuario, ct);
        if (usuarioResult.IsFailure || usuarioResult.Value is null)
            return NotFound(new { codigo = "USUARIO_NO_ENCONTRADO" });

        var membresiaResult = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(request.IdUsuario, request.IdTenant, ct);
        if (membresiaResult.IsFailure || membresiaResult.Value == null || membresiaResult.Value.IdEstado != (int)EEstadoUsuario.Activo)
            return Forbid();

        var result = await _service.ValidarMFAAsync(
            request.IdUsuario, _tenantContext.CurrentId, request.IdTipoMFA, request.IdMFA, ct);

        if (result.IsFailure)
            return Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

        return Ok(new { success = true });
    }

    [HttpGet("metodos/{idUsuario}")]
    public async Task<IActionResult> GetMetodos(int idUsuario, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ObtenerMetodosVisiblesAsync(uid.Value, idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("metodo-principal/{idUsuario}")]
    public async Task<IActionResult> GetMetodoPrincipal(int idUsuario, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var acceso = await _visibilidad.VerificarAsync(uid.Value, idUsuario, _tenantContext.CurrentId, "métodos MFA del usuario", ct);
        if (acceso.IsFailure) return FromResult(acceso);

        return FromResultQuery(await _service.ObtenerMetodoPrincipalAsync(idUsuario, ct));
    }

    // S120 · D3 — el propio usuario; otro, con MFA_ADMINISTRAR (o sistema) y miembro del tenant de la sesión; si no, 403
    [HttpPost("{idUsuario}/revocar/{idMFARegistro}")]
    public async Task<IActionResult> Revocar(int idUsuario, int idMFARegistro, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        var puedeAdministrar = User.EsSistema() || User.HasClaim("permiso", "MFA_ADMINISTRAR");
        var acceso = await _visibilidad.VerificarAdministracionAsync(uid.Value, idUsuario, _tenantContext.CurrentId, puedeAdministrar, "MFA de otro usuario", ct);
        if (acceso.IsFailure) return FromResult(acceso);

        var result = await _service.RevocarMetodoAsync(idUsuario, idMFARegistro, null, null, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorAlGuardarMfa("revocar", ex);
        }
        return NoContent();
    }
}

public class ValidarMfaRequest
{
    [Required] public int    IdUsuario { get; init; }
    [Required] public int    IdTenant  { get; init; }
    [Required] public int    IdTipoMFA { get; init; }
    [Required(AllowEmptyStrings = false)] public string IdMFA { get; init; } = string.Empty;
}
