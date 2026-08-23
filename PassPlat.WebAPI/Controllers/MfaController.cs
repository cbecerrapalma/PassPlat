using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Enums;

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

    public MfaController(IMFAService service, ITenantContext tenantContext, IUnitOfWorkAsync uow, IUsuarioRepository usuarioRepo, IUsuarioTenantRepository usuarioTenantRepo)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
        _usuarioRepo = usuarioRepo;
        _usuarioTenantRepo = usuarioTenantRepo;
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
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ObtenerMetodosPorUsuarioAsync(idUsuario, ct));
    }

    [HttpGet("metodo-principal/{idUsuario}")]
    public async Task<IActionResult> GetMetodoPrincipal(int idUsuario, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ObtenerMetodoPrincipalAsync(idUsuario, ct));
    }

    [HttpPost("registrar")]
    [Authorize(Policy = "MFA_ADMINISTRAR")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarMFADto dto, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != dto.IdUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.RegistrarMFAAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Conflict(new { codigo = "ERROR_AL_GUARDAR_MFA", mensaje = ex.InnerException?.Message ?? ex.Message });
        }
        return Ok(result.Value);
    }

    [HttpPost("{idUsuario}/revocar/{idMFARegistro}")]
    public async Task<IActionResult> Revocar(int idUsuario, int idMFARegistro, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var result = await _service.RevocarMetodoAsync(idUsuario, idMFARegistro, null, null, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Conflict(new { codigo = "ERROR_AL_GUARDAR_MFA", mensaje = ex.InnerException?.Message ?? ex.Message });
        }
        return NoContent();
    }

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;
}

public class ValidarMfaRequest
{
    [Required] public int    IdUsuario { get; init; }
    [Required] public int    IdTenant  { get; init; }
    [Required] public int    IdTipoMFA { get; init; }
    [Required(AllowEmptyStrings = false)] public string IdMFA { get; init; } = string.Empty;
}
