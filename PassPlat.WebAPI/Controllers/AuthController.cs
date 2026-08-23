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

namespace PassPlat.WebAPI.Controllers;

[EnableRateLimiting("LoginPolicy")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _service;
    private readonly IUsuarioService _usuarioService;
    private readonly ITokenRestService _tokenRestService;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordService _passwordService;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;

    public AuthController(
        IAuthService service,
        IUsuarioService usuarioService,
        ITokenRestService tokenRestService,
        ITenantContext tenantContext,
        IPasswordService passwordService,
        ITenantService tenantService,
        IUnitOfWorkAsync uow,
        IUsuarioTenantRepository usuarioTenantRepo)
    {
        _service = service;
        _usuarioService = usuarioService;
        _tokenRestService = tokenRestService;
        _tenantContext = tenantContext;
        _passwordService = passwordService;
        _tenantService = tenantService;
        _uow = uow;
        _usuarioTenantRepo = usuarioTenantRepo;
    }

    [AllowAnonymous]
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
                return Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

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
        catch (Exception ex)
        {
            return StatusCode(500, new { codigo = "LOGIN_ERROR", mensaje = ex.Message });
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
        var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
            return BadRequest(new { codigo = "INVALID_TOKEN" });

        var result = await _service.RevocarSesionPorJtiAsync(idUsuario, jti, ct);
        if (result.IsSuccess) await _uow.SaveChangesAsync(ct);
        return result.IsSuccess ? Ok(new { mensaje = "Sesión cerrada" }) : FromResult(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
            return Unauthorized(new { codigo = "INVALID_TOKEN" });

        var result = await _usuarioService.GetByIdAsync(idUsuario, ct);
        return FromResult(result);
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
                return Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { codigo = "PLATFORM_LOGIN_ERROR", mensaje = ex.Message });
        }
    }

    [HttpPost("switch-tenant/{idTenant}")]
    public async Task<IActionResult> SwitchTenant(int idTenant, [FromBody] SwitchTenantRequest request, CancellationToken ct)
    {
        try
        {
            var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
                return Unauthorized(new { codigo = "INVALID_TOKEN" });

            var result = await _service.SwitchTenantAsync(
                idUsuario, idTenant, request.IdApp,
                request.IdDisp, request.IdIP, request.IdAgente, ct);

            if (result.IsFailure)
                return Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { codigo = "SWITCH_TENANT_ERROR", mensaje = ex.Message });
        }
    }

    [HttpPost("switch-to-platform")]
    public async Task<IActionResult> SwitchToPlatform([FromBody] SwitchToPlatformRequest request, CancellationToken ct)
    {
        try
        {
            var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
                return Unauthorized(new { codigo = "INVALID_TOKEN" });

            var jtiClaim = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jtiClaim))
                return Unauthorized(new { codigo = "INVALID_TOKEN", mensaje = "JTI claim no encontrado" });

            var result = await _service.SwitchToPlatformAsync(
                idUsuario, jtiClaim, request.IdApp,
                request.IdDisp, request.IdIP, request.IdAgente, ct);

            if (result.IsFailure)
            {
                if (result.Error!.Code == "SIN_ACCESO_PLATFORM")
                    return Forbid();
                return Unauthorized(new { codigo = result.Error.Code, mensaje = result.Error.Message });
            }

            await _uow.SaveChangesAsync(ct);

            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { codigo = "SWITCH_PLATFORM_ERROR", mensaje = ex.Message });
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
            return Unauthorized(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

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

        var hashResult = await _passwordService.HashPasswordAsync(request.NuevaPassword, ct);
        if (hashResult.IsFailure)
            return BadRequest(new { codigo = "PWD_HASH_ERROR", mensaje = "Error al procesar la nueva contraseña." });

        var cambioResult = await _passwordService.CambiarPasswordAsync(
            idUsuario, idTenant, hashResult.Value, pepperVersion: 1,
            idTipoCambio: (int)PassPlat.Dominio.Enums.ETipoCambioPwd.Reset,
            idDisp: null, idIP: null, idAgente: null, ct);

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
        var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idUsuarioClaim) || !int.TryParse(idUsuarioClaim, out var idUsuario))
            return Unauthorized(new { codigo = "INVALID_TOKEN" });

        var result = await _usuarioTenantRepo.ObtenerActivosPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure)
            return FromResult(result);

        var tenants = result.Value
            .Where(ut => ut.Tenant != null)
            .Select(ut => new
            {
                id = ut.IdTenant,
                codigo = ut.Tenant!.Codigo,
                nombre = ut.Tenant!.Nombre
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

public class SwitchToPlatformRequest
{
    [Required]
    public int IdApp { get; init; }
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
}
