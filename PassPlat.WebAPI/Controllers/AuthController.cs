using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

// S63: las acciones sin limitador propio (me, logout, tenants, switch-*...) usan AuthReadPolicy;
// login, login/platform y validar-mfa mantienen LoginPolicy en su propio atributo.
[EnableRateLimiting("AuthReadPolicy")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _service;
    private readonly IUsuarioService _usuarioService;
    private readonly ITokenRestService _tokenRestService;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordService _passwordService;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService service,
        IUsuarioService usuarioService,
        ITokenRestService tokenRestService,
        ITenantContext tenantContext,
        IPasswordService passwordService,
        ITenantService tenantService,
        IUnitOfWorkAsync uow,
        ILogger<AuthController> logger)
    {
        _service = service;
        _usuarioService = usuarioService;
        _tokenRestService = tokenRestService;
        _tenantContext = tenantContext;
        _passwordService = passwordService;
        _tenantService = tenantService;
        _uow = uow;
        _logger = logger;
    }

    // S65: una excepción inesperada no se devuelve al cliente; queda en el log con el traceId.
    private const string MensajeErrorInterno = "Error interno. Intente nuevamente más tarde.";

    private IActionResult ErrorInterno(string codigo, Exception ex)
    {
        _logger.LogError(ex, "Error interno {Codigo} trace {TraceId}", codigo, HttpContext.TraceIdentifier);
        return StatusCode(500, new { codigo, mensaje = MensajeErrorInterno, traceId = HttpContext.TraceIdentifier });
    }
    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.LoginConTokenAsync(
                request.NomUsuario, request.Email, request.IdApp,
                request.Password, request.IdTenant, request.IdDisp, request.IdIP,
                request.IdAgente, ct);

            await _uow.SaveChangesAsync(ct);

            if (result.IsFailure)
                return FromLoginFailure(result.Error!.Code, result.Error.Message);

            var response = result.Value;

            if (response.IdMFAPrincipal.HasValue)
                return Ok(new
                {
                    requiereMFA = true,
                    idMFAPrincipal = response.IdMFAPrincipal.Value,
                    idTipoMFA = response.IdTipoMFA,
                    idUsuario = response.IdUsuario,
                    idTenant = response.IdTenant,
                    nomUsuario = response.NomUsuario
                });

            return Ok(response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("LOGIN_ERROR", ex);
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("RefreshPolicy")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var result = await _service.RefreshTokenAsync(request.RefreshToken, ct);
        if (result.IsSuccess) await _uow.SaveChangesAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(jti) || User.IdUsuario() is not int idUsuario)
            return BadRequest(new { codigo = "INVALID_TOKEN" });

        var result = await _service.RevocarSesionPorJtiAsync(idUsuario, jti, ct);
        if (result.IsSuccess) await _uow.SaveChangesAsync(ct);
        return result.IsSuccess ? Ok(new { mensaje = "Sesión cerrada" }) : FromResult(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (User.IdUsuario() is not int idUsuario)
            return Unauthorized(new { codigo = "INVALID_TOKEN" });

        var result = await _usuarioService.GetByIdAsync(idUsuario, ct);
        return FromResult(result);
    }

    // S57.19 H-1+H-3+H-5: mapeo estable de errores de login.
    // LOGIN_VALIDATION (IdApp/IdTenant/app/tenant) → 400; onboarding/estado → 403;
    // errores de validación contra DB → 500; credenciales → 401.
    // S60 · D5: PWD_EXPIRADA (contraseña vencida) → 403.
    private static IActionResult FromLoginFailure(string? code, string? message)
    {
        return code switch
        {
            "LOGIN_VALIDATION" => new BadRequestObjectResult(new { codigo = code, mensaje = message }),
            "PWD_EXPIRADA" or "SIN_ACCESO_APP" or "SIN_ACCESO_PLATFORM" or "SIN_ACCESO_TENANT" or "CUENTA_INACTIVA" => new ObjectResult(new { codigo = code, mensaje = message }) { StatusCode = 403 },
            "APP_VALIDATION_ERROR" or "TENANT_VALIDATION_ERROR" => new ObjectResult(new { codigo = code, mensaje = message }) { StatusCode = 500 },
            _ => new UnauthorizedObjectResult(new { codigo = code, mensaje = message }),
        };
    }

    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login/platform")]
    public async Task<IActionResult> PlatformLogin([FromBody] PlatformLoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.PlatformLoginAsync(
                request.NomUsuario, request.Password, request.IdApp,
                request.IdDisp, request.IdIP, request.IdAgente, ct);

            if (result.IsFailure)
                return FromLoginFailure(result.Error!.Code, result.Error.Message);

            return Ok(result.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("PLATFORM_LOGIN_ERROR", ex);
        }
    }

    [HttpPost("switch-tenant/{idTenant}")]
    public async Task<IActionResult> SwitchTenant(int idTenant, [FromBody] SwitchTenantRequest request, CancellationToken ct)
    {
        try
        {
            if (User.IdUsuario() is not int idUsuario)
                return Unauthorized(new { codigo = "INVALID_TOKEN" });

            var result = await _service.SwitchTenantAsync(
                idUsuario, idTenant, request.IdApp,
                request.IdDisp, request.IdIP, request.IdAgente, ct);

            if (result.IsFailure)
                return FromLoginFailure(result.Error!.Code, result.Error.Message);

            return Ok(result.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("SWITCH_TENANT_ERROR", ex);
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("TokenPolicy")]
    [HttpPost("olvido-password")]
    public async Task<IActionResult> OlvidoPassword([FromBody] SolicitarResetPasswordDto request, CancellationToken ct)
    {
        var tenantId = request.IdTenant;
        var idApp = request.IdApp ?? 1;

        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.NomUsuario))
            return BadRequest(new { codigo = "IDENTIFICADOR_REQUERIDO", mensaje = "Debe proporcionar email o nombre de usuario" });

        UsuarioDto? usuario = null;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var result = await _usuarioService.ObtenerPorEmailAsync(tenantId, request.Email, ct);
            if (result.IsSuccess) usuario = result.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.NomUsuario))
        {
            var result = await _usuarioService.ObtenerPorNomUsuarioAsync(tenantId, request.NomUsuario, ct);
            if (result.IsSuccess) usuario = result.Value;
        }

        // Security: same response for all negative cases (prevents enumeration)
        if (usuario == null || usuario.Eliminado)
        {
            return Ok(new PasswordResetResponseDto
            {
                Message = "Si el usuario existe y tiene correo electrónico verificado, recibirás instrucciones para restablecer la contraseña"
            });
        }

        // OAuth-only users cannot reset password — they must use their provider
        if (!usuario.TienePasswordLocal)
        {
            return Ok(new PasswordResetResponseDto
            {
                Message = "Esta cuenta utiliza autenticación mediante un proveedor externo. Debe recuperar su acceso directamente con dicho proveedor.",
                RequiresExternalAuth = true
            });
        }

        if (string.IsNullOrWhiteSpace(usuario.Email) || !usuario.EmailVerificado)
        {
            return Ok(new PasswordResetResponseDto
            {
                Message = "El usuario no tiene un correo electrónico verificado para recuperación de contraseña"
            });
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenPlano = Convert.ToHexStringLower(tokenBytes);
        var hashToken = Convert.ToHexStringLower(SHA256.HashData(tokenBytes));
        var fecVence = DateTime.Now.AddHours(1);

        var baseUrl = request.ResetUrl ?? $"{Request.Scheme}://{Request.Host}/reset-password";

        await _tokenRestService.GenerarTokenResetPasswordAsync(
            usuario.Id, usuario.IdTenant, idApp, tokenPlano, hashToken,
            fecVence, baseUrl, idDisp: null, idIP: null, idAgente: null, ct);

        await _uow.SaveChangesAsync(ct);

        return Ok(new PasswordResetResponseDto
        {
            Message = "Si el usuario existe y tiene correo electrónico verificado, recibirás instrucciones para restablecer la contraseña"
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("validar-mfa")]
    public async Task<IActionResult> ValidarMFA([FromBody] ValidarMfaLoginRequest request, CancellationToken ct)
    {
        var result = await _service.CompletarLoginConMFAAsync(
            request.IdUsuario, request.IdTenant, request.IdApp,
            request.IdMFAPrincipal, request.CodigoMFA,
            request.IdDisp, request.IdIP, request.IdAgente, ct);

        await _uow.SaveChangesAsync(ct);

        if (result.IsFailure)
            return FromLoginFailure(result.Error!.Code, result.Error.Message);

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("TokenPolicy")]
    [HttpPost("restablecer-password")]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDto request, CancellationToken ct)
    {
        var hashToken = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Token)));

        var validarResult = await _tokenRestService.ValidarTokenAsync(hashToken, request.IdApp, ct);
        if (validarResult.IsFailure)
            return BadRequest(new { codigo = "TOKEN_INVALIDO", mensaje = "El token es inválido o ha expirado." });

        if (validarResult.Value.Exito != 1 || !validarResult.Value.IdUsuario.HasValue || !validarResult.Value.IdTenant.HasValue)
            return BadRequest(new { codigo = "TOKEN_INVALIDO", mensaje = "El token es inválido o ha expirado." });

        var idUsuario = validarResult.Value.IdUsuario.Value;
        var idTenant = validarResult.Value.IdTenant.Value;

        var cambioResult = await _passwordService.CambiarPasswordValidadaAsync(
            idUsuario, idTenant, request.IdApp, request.NuevaPassword,
            PassPlat.Dominio.Enums.ETipoCambioPwd.Reset, ct);

        if (cambioResult.IsSuccess) await _uow.SaveChangesAsync(ct);

        if (cambioResult.IsFailure)
            return BadRequest(new { codigo = cambioResult.Error!.Code, mensaje = cambioResult.Error.Message });

        return Ok(new RestablecerPasswordResponseDto());
    }

    [Authorize]
    [HttpGet("current-tenant")]
    public async Task<IActionResult> GetCurrentTenant(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentId;
        var tenant = await _tenantService.ObtenerPorIdAsync(tenantId, ct);
        if (tenant.IsFailure || tenant.Value == null)
            return NotFound(new { mensaje = "Tenant no encontrado" });
        return Ok(new { id = tenant.Value.Id, nombre = tenant.Value.Nombre, codigo = tenant.Value.Codigo });
    }

    [AllowAnonymous]
    [HttpGet("tenant-info")]
    public async Task<IActionResult> GetTenantInfo(CancellationToken ct)
    {
        var resolvedTenantId = HttpContext.Items["ResolvedTenantId"] as int?;
        if (resolvedTenantId.HasValue)
        {
            var tenant = await _tenantService.ObtenerPorIdAsync(resolvedTenantId.Value, ct);
            if (tenant.IsSuccess && tenant.Value != null)
                return Ok(new { idTenant = resolvedTenantId, nombreTenant = tenant.Value.Nombre, codigoTenant = tenant.Value.Codigo, requiereSeleccion = false });
        }
        return Ok(new { idTenant = (int?)null, nombreTenant = (string?)null, requiereSeleccion = true });
    }

    [AllowAnonymous]
    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var result = await _tenantService.ObtenerActivosAsync(ct);
        if (result.IsFailure)
            return FromResult(result);
        return Ok(result.Value);
    }

    [HttpGet("mis-tenants")]
    public async Task<IActionResult> GetMisTenants(CancellationToken ct)
    {
        if (User.IdUsuario() is not int idUsuario)
            return Unauthorized(new { codigo = "INVALID_TOKEN" });

        // S97 · D1 — la regla vive en IAuthService.ObtenerMisTenantsAsync; la forma de la respuesta no cambia
        var result = await _service.ObtenerMisTenantsAsync(idUsuario, ct);
        if (result.IsFailure)
            return FromResult(result);

        var tenants = result.Value
            .Select(t => new
            {
                id = t.Id,
                codigo = t.Codigo,
                nombre = t.Nombre
            })
            .ToList();

        return Ok(tenants);
    }
}

public class ValidarMfaLoginRequest
{
    [Required] public int IdUsuario { get; init; }
    [Required] public int IdTenant { get; init; }
    [Required] public int IdApp { get; init; }
    [Required] public int IdMFAPrincipal { get; init; }
    [Required(AllowEmptyStrings = false)] public string CodigoMFA { get; init; } = string.Empty;
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
}

public class LoginRequest
{
    public string? NomUsuario { get; init; }
    public string? Email { get; init; }
    [Required]
    public int IdApp { get; init; }
    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
    [Required]
    public int IdTenant { get; init; }
}

public class PlatformLoginRequest
{
    [Required(AllowEmptyStrings = false)]
    public string NomUsuario { get; init; } = string.Empty;
    [Required]
    public int IdApp { get; init; }
    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
}

public class SwitchTenantRequest
{
    [Required]
    public int IdApp { get; init; }
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
}
