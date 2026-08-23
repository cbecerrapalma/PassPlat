using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.Logging;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;


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
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != request.IdUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        if (string.IsNullOrEmpty(request.PasswordActual))
            return BadRequest(new { codigo = "CURRENT_PASSWORD_REQUIRED", mensaje = "La contraseña actual es obligatoria" });

        var validarResult = await _service.ValidarPasswordActualAsync(request.IdUsuario, request.PasswordActual, ct);
        if (validarResult.IsFailure)
            return Unauthorized(new { codigo = validarResult.Error!.Code, mensaje = validarResult.Error.Message });

        var result = await _service.CambiarPasswordAsync(
            request.IdUsuario, _tenantContext.CurrentId, request.HashPwdNuevo,
            request.PepperVersion, request.IdTipoCambio, request.IdDisp,
            request.IdIP, request.IdAgente, ct);

        if (result.IsSuccess) await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("validar-repetida")]
    public async Task<IActionResult> ValidarRepetida([FromBody] ValidarRepetidaRequest request, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != request.IdUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResult(await _service.ValidarPasswordRepetidaAsync(
            request.IdUsuario, request.HashPwd, request.HistorialCant, ct));
    }

    [HttpPost("{idUsuario}/desactivar-actual")]
    public async Task<IActionResult> DesactivarActual(int idUsuario, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var result = await _service.DesactivarPasswordActualAsync(idUsuario, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("calcular-hash")]
    public async Task<IActionResult> CalcularHash([FromBody] CalcularHashRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { codigo = "PASSWORD_REQUIRED", mensaje = "La contraseña es obligatoria" });

        var result = await _service.HashPasswordAsync(request.Password, ct);
        return result.IsSuccess ? Ok(new { hash = result.Value }) : FromResult(result);
    }

    [HttpPost("trigger-expiration")]
    public async Task<IActionResult> TriggerExpiration([FromBody] TriggerExpirationRequest request, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (!idUsuarioJwt.HasValue)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var emailQueue = HttpContext.RequestServices.GetRequiredService<IEmailQueue>();
        var historialRepo = HttpContext.RequestServices.GetRequiredService<PassPlat.Datos.Repositories.IHistorialPwdRepository>();
        var usuarioRepo = HttpContext.RequestServices.GetRequiredService<PassPlat.Datos.Repositories.IUsuarioRepository>();

        var usuarioResult = await usuarioRepo.GetByIdAsync(request.IdUsuario, ct);
        if (usuarioResult.IsFailure)
            return NotFound(new { codigo = "USUARIO_NO_ENCONTRADO" });

        var usuario = usuarioResult.Value;
        var historialResult = await historialRepo.ObtenerHistorialRecienteAsync(request.IdUsuario, 1, ct);
        if (historialResult.IsFailure || historialResult.Value.Count == 0)
            return BadRequest(new { codigo = "SIN_HISTORIAL", mensaje = "El usuario no tiene historial de contraseñas" });

        var passwordActual = historialResult.Value[0];
        var diasRestantes = passwordActual.FecExpira.HasValue
            ? (int)(passwordActual.FecExpira.Value - DateTime.Now).TotalDays
            : 0;

        var correlationId = HttpContext.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string;
        await emailQueue.EnqueueAsync(new PassPlat.Aplicacion.Services.Email.EmailJob(
            PassPlat.Aplicacion.Services.Email.EmailJobKind.PasswordExpired,
            usuario.Email ?? usuario.NomUsuario + "@test.com",
            usuario.NomUsuario,
            new Dictionary<string, object?>
            {
                ["DiasRestantes"] = diasRestantes <= 0 ? 0 : diasRestantes,
                ["FechaExpira"] = passwordActual.FecExpira?.ToString("dd/MM/yyyy") ?? "Desconocida",
                ["TipoEvento"] = diasRestantes <= 0 ? "PasswordExpired" : $"PasswordExpirationWarning_{diasRestantes}d",
                ["TemplateCode"] = diasRestantes <= 0 ? "password-expired" : $"password-expiration-{diasRestantes}",
                ["AppName"] = "PassPlat"
            },
            IdTenant: request.IdTenant,
            IdUsuario: request.IdUsuario,
            CorrelationId: correlationId), ct);

        _ = await _uow.SaveChangesAsync(ct);
        return Ok(new { mensaje = "Expiración encolada", diasRestantes });
    }

    [HttpPost("trigger-first-login")]
    public async Task<IActionResult> TriggerFirstLogin([FromBody] TriggerFirstLoginRequest request, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (!idUsuarioJwt.HasValue)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var emailQueue = HttpContext.RequestServices.GetRequiredService<IEmailQueue>();

        var correlationId = HttpContext.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string;
        await emailQueue.EnqueueAsync(new PassPlat.Aplicacion.Services.Email.EmailJob(
            PassPlat.Aplicacion.Services.Email.EmailJobKind.FirstLogin,
            string.Empty,
            string.Empty,
            new Dictionary<string, object?>
            {
                ["IdUsuario"] = request.IdUsuario,
                ["IdTenant"] = request.IdTenant,
                ["AppName"] = "PassPlat"
            },
            IdTenant: request.IdTenant,
            IdUsuario: request.IdUsuario,
            CorrelationId: correlationId), ct);

        return Ok(new { mensaje = "FirstLogin encolado" });
    }

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;
}

public class TriggerFirstLoginRequest
{
    [Required]
    public int IdUsuario { get; init; }
    [Required]
    public int IdTenant { get; init; }
}

public class CambiarPasswordRequest
{
    [Required]
    public int     IdUsuario      { get; init; }
    [Required]
    public int     IdTenant       { get; init; }
    [Required(AllowEmptyStrings = false)]
    public string  PasswordActual  { get; init; } = string.Empty;
    [Required(AllowEmptyStrings = false)]
    public string  HashPwdNuevo   { get; init; } = string.Empty;
    public byte    PepperVersion  { get; init; }
    public int     IdTipoCambio   { get; init; }
    public int?    IdDisp         { get; init; }
    public int?    IdIP           { get; init; }
    public int?    IdAgente       { get; init; }
}

public class ValidarRepetidaRequest
{
    [Required]
    public int     IdUsuario     { get; init; }
    [Required(AllowEmptyStrings = false)]
    public string  HashPwd       { get; init; } = string.Empty;
    [Required]
    public int     HistorialCant { get; init; }
}

public class CalcularHashRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;
}

public class TriggerExpirationRequest
{
    [Required]
    public int IdUsuario { get; init; }
    [Required]
    public int IdTenant { get; init; }
}
