using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.Logging;
using CBP.MultiTenant.Abstractions;
using CBP.Results.Errors;
using CBP.Results;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.WebAPI.Auth;


namespace PassPlat.WebAPI.Controllers;

[Authorize]
[EnableRateLimiting("PasswordPolicy")]
public class PasswordController : BaseApiController
{
    private readonly PassPlat.Aplicacion.Services.IPasswordService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public PasswordController(
        PassPlat.Aplicacion.Services.IPasswordService service,
        ITenantContext tenantContext,
        IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpPost("cambiar")]
    public async Task<IActionResult> Cambiar([FromBody] CambiarPasswordRequest request, CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();
        if (idUsuario is null)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        if (string.IsNullOrEmpty(request.PasswordActual))
            return BadRequest(new { codigo = "CURRENT_PASSWORD_REQUIRED", mensaje = "La contraseña actual es obligatoria" });

        var validarResult = await _service.ValidarPasswordActualAsync(idUsuario.Value, request.PasswordActual, ct);
        if (validarResult.IsFailure)
            return Unauthorized(new { codigo = validarResult.Error!.Code, mensaje = validarResult.Error.Message });

        var result = await _service.CambiarPasswordValidadaAsync(
            idUsuario.Value, _tenantContext.CurrentId, idApp: null, request.PasswordNueva,
            PassPlat.Dominio.Enums.ETipoCambioPwd.Voluntario, ct);

        if (result.IsFailure)
            return BadRequest(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
}

public class CambiarPasswordRequest
{
    [Required(AllowEmptyStrings = false)]
    public string PasswordActual { get; init; } = string.Empty;
    [Required(AllowEmptyStrings = false)]
    public string PasswordNueva { get; init; } = string.Empty;
}
