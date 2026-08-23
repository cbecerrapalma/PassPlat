using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.Logging;
using Microsoft.EntityFrameworkCore;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VER")]
public class UsuariosController : BaseApiController
{
    private readonly IUsuarioService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordService _passwordService;
    private readonly IPoliticaPwdService _politicaPwdService;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IValidator<CrearUsuarioDto> _crearValidator;
    private readonly IEmailQueue _emailQueue;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;

    public UsuariosController(IUsuarioService service, ITenantContext tenantContext, IPasswordService passwordService, IPoliticaPwdService politicaPwdService, IUnitOfWorkAsync uow, IValidator<CrearUsuarioDto> crearValidator, IEmailQueue emailQueue, IUsuarioTenantRepository usuarioTenantRepo)
    {
        _service = service;
        _tenantContext = tenantContext;
        _passwordService = passwordService;
        _politicaPwdService = politicaPwdService;
        _uow = uow;
        _crearValidator = crearValidator;
        _emailQueue = emailQueue;
        _usuarioTenantRepo = usuarioTenantRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (_tenantContext.HasTenant)
        {
            var options = new PaginationOptions<Usuario>(pageNumber: 1, pageSize: 200);
            var pagedResult = await _service.ObtenerPaginadoPorTenantAsync(_tenantContext.CurrentId, options, null, ct);
            if (pagedResult.IsFailure) return FromResult(pagedResult);
            return Ok(pagedResult.Value.Items);
        }
        return FromResultQuery(await _service.GetAllAsync(ct: ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        if (_tenantContext.HasTenant && !await EsMiembroTenantActualAsync(id, ct))
            return NotFound();
        return FromResultQuery(await _service.ObtenerCompletoPorIdAsync(id, ct));
    }

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, CancellationToken ct)
    {
        var options = new PaginationOptions<Usuario>(request.PageNumber, request.PageSize);
        if (!User.HasClaim("is_system", "true"))
        {
            var tenantResult = await _service.ObtenerPaginadoPorTenantAsync(_tenantContext.CurrentId, options, search, ct);
            return tenantResult.IsSuccess ? Ok(ToPagedResponse(tenantResult.Value)) : FromResult(tenantResult);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchResult = await _service.ObtenerPaginadoConBusquedaAsync(options, search, ct);
            if (searchResult.IsFailure) return FromResult(searchResult);
            return Ok(ToPagedResponse(searchResult.Value));
        }
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count(CancellationToken ct)
        => FromResultQuery(await _service.CountAsync(ct));

    [HttpGet("nomusuario/{nomUsuario}")]
    public async Task<IActionResult> GetByNomUsuario(string nomUsuario, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerPorNomUsuarioAsync(_tenantContext.CurrentId, nomUsuario, ct));
    }

    [HttpGet("email/{email}")]
    public async Task<IActionResult> GetByEmail(string email, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerPorEmailAsync(_tenantContext.CurrentId, email, ct));
    }

    [HttpGet("con-intentos-excedidos")]
    public async Task<IActionResult> GetConIntentosExcedidos(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerConIntentosExcedidosAsync(_tenantContext.CurrentId, 5, ct));

    [HttpGet("con-password-expirada")]
    public async Task<IActionResult> GetConPasswordExpirada(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerConPasswordExpiradaAsync(_tenantContext.CurrentId, 90, ct));

    [HttpPost]
    [Authorize(Policy = "USUARIOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearUsuarioDto dto, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        dto.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email;

        var validation = await _crearValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(new { codigo = "VALIDATION_ERROR", mensaje = errors });
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            var result = await _service.CrearAsync(dto, ct);
            if (result.IsFailure) return FromResult(result);
            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return BadRequest(new { codigo = "DB_CONSTRAINT", mensaje = msg });
            }
            await _service.EnviarBienvenidaAsync(dto.Email, dto.NomUsuario, dto.IdTenant, result.Value.Id, dto.IdApp, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
        }

        var policyResult = await _politicaPwdService.ObtenerPoliticaAplicableAsync(_tenantContext.CurrentId, idApp: null, ct);
        var cbpPolicy = PoliticaPwdDtoToCbpPolicy(policyResult.IsSuccess ? policyResult.Value! : null);
        var hashResult = await _passwordService.HashPasswordAsync(dto.Password, cbpPolicy, ct);
        if (hashResult.IsFailure)
            return BadRequest(new { codigo = "PASSWORD_POLICY_FAILED", mensaje = hashResult.Error!.Message });

        var spResult = await _service.CrearConPasswordAsync(
            dto.IdTenant, dto.IdEstado, dto.NomUsuario, dto.Email, dto.Nombre, dto.Apellido,
            hashPwd: hashResult.Value, ct: ct);

        if (spResult.IsFailure)
            return BadRequest(new { codigo = "CREACION_FALLIDA", mensaje = spResult.Error!.Message });

        if (spResult.Value.Resultado != 0)
            return BadRequest(new { codigo = $"SP_ERROR_{spResult.Value.Resultado}", mensaje = spResult.Value.Mensaje });

        var usuario = await _service.ObtenerCompletoPorIdAsync(spResult.Value.IdUsuario!.Value, ct);
        await _service.EnviarBienvenidaAsync(dto.Email, dto.NomUsuario, dto.IdTenant, spResult.Value.IdUsuario.Value, dto.IdApp, ct);

        if (usuario.IsFailure)
            return CreatedAtAction(nameof(GetById), new { id = spResult.Value.IdUsuario.Value }, null);

        return CreatedAtAction(nameof(GetById), new { id = spResult.Value.IdUsuario.Value }, usuario.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "USUARIOS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarUsuarioDto dto, CancellationToken ct)
    {
        if (dto.Id != id) return BadRequest(new { codigo = "ID_MISMATCH", mensaje = "El ID de la URL no coincide con el ID del cuerpo" });
        if (_tenantContext.HasTenant && !await EsMiembroTenantActualAsync(id, ct))
            return NotFound();
        var result = await _service.ActualizarAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { codigo = "DB_CONSTRAINT", mensaje = msg });
        }
        return FromResult(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "USUARIOS_ELIMINAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == id) return BadRequest(new { codigo = "AUTO_ELIMINACION" });
        if (_tenantContext.HasTenant && !await EsMiembroTenantActualAsync(id, ct))
            return NotFound();
        var result = await _service.MarcarEliminadoAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { codigo = "DB_CONSTRAINT", mensaje = msg });
        }
        return FromResult(result);
    }

    [HttpGet("password-policy")]
    public async Task<IActionResult> GetPasswordPolicy(CancellationToken ct)
    {
        var policy = await _politicaPwdService.ObtenerPoliticaAplicableAsync(_tenantContext.CurrentId, idApp: null, ct);
        if (policy.IsFailure || policy.Value == null)
            return Ok(new PoliticaPwdDto { LongMin = 8, LongMax = 128, ReqMayuscula = true, ReqMinuscula = true, ReqNumero = true, ReqEspecial = false });

        return Ok(policy.Value);
    }

    [HttpPost("validar-password")]
    public async Task<IActionResult> ValidarPassword([FromBody] ValidarPasswordRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { codigo = "PASSWORD_REQUIRED" });

        var policyResult = await _politicaPwdService.ObtenerPoliticaAplicableAsync(_tenantContext.CurrentId, idApp: null, ct);
        var policy = policyResult.IsSuccess && policyResult.Value != null
            ? policyResult.Value
            : new PoliticaPwdDto { LongMin = 8, LongMax = 128, ReqMayuscula = true, ReqMinuscula = true, ReqNumero = true };

        var result = await _passwordService.ValidarPasswordFortalezaAsync(request.Password, policy, request.NomUsuario, request.Email, ct);
        if (result.IsFailure)
            return Ok(new PasswordStrengthInfoDto { IsValid = false, Score = 0, Level = "Error", Errors = [result.Error!.Message] });

        return Ok(result.Value);
    }

    [HttpPost("{id}/cambiar-password-admin")]
    public async Task<IActionResult> CambiarPasswordAdmin(int id, [FromBody] CambiarPwdAdminRequest request, CancellationToken ct)
    {
        if (!User.HasClaim("is_system", "true"))
            return Unauthorized(new { codigo = "ACCESO_DENEGADO", mensaje = "Solo usuarios de sistema pueden cambiar contraseñas de otros usuarios" });

        if (string.IsNullOrWhiteSpace(request.NuevaPassword))
            return BadRequest(new { codigo = "PASSWORD_REQUIRED", mensaje = "La nueva contraseña es obligatoria" });

        var idUsrEjecutor = GetIdUsuario();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown";
        var correlationId = HttpContext.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string ?? Guid.NewGuid().ToString("N");

        var result = await _passwordService.AdminCambiarPasswordAsync(id, _tenantContext.CurrentId, request.NuevaPassword, idUsrEjecutor, ipAddress, userAgent, correlationId, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("{id}/agregar-password-local")]
    [Authorize(Policy = "USUARIOS_EDITAR")]
    public async Task<IActionResult> AgregarPasswordLocal(int id, [FromBody] AgregarPasswordLocalRequest request, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        // Users can only add password to themselves, or admins to any user
        if (uid != id && !User.HasClaim("is_system", "true"))
            return Unauthorized(new { codigo = "ACCESO_DENEGADO", mensaje = "Solo puede agregar contraseña a su propia cuenta" });

        if (_tenantContext.HasTenant && !await EsMiembroTenantActualAsync(id, ct))
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.NuevaPassword))
            return BadRequest(new { codigo = "PASSWORD_REQUIRED", mensaje = "La nueva contraseña es obligatoria" });

        // Verify user exists and is not deleted
        var usuarioResult = await _service.GetByIdAsync(id, ct);
        if (usuarioResult.IsFailure || usuarioResult.Value == null)
            return NotFound(new { codigo = "USER_NOT_FOUND", mensaje = "Usuario no encontrado" });

        var usuario = usuarioResult.Value;
        if (usuario.Eliminado)
            return BadRequest(new { codigo = "USER_DELETED", mensaje = "Usuario eliminado" });

        // If user already has local password, use change password flow instead
        if (usuario.TienePasswordLocal)
            return BadRequest(new { codigo = "ALREADY_HAS_PASSWORD", mensaje = "El usuario ya tiene contraseña local. Use el flujo de cambio de contraseña." });

        // Validate password against policy
        var policyResult = await _politicaPwdService.ObtenerPoliticaAplicableAsync(_tenantContext.CurrentId, idApp: null, ct);
        var cbpPolicy = PoliticaPwdDtoToCbpPolicy(policyResult.IsSuccess ? policyResult.Value! : null);
        var hashResult = await _passwordService.HashPasswordAsync(request.NuevaPassword, cbpPolicy, ct);
        if (hashResult.IsFailure)
            return BadRequest(new { codigo = "PASSWORD_POLICY_FAILED", mensaje = hashResult.Error!.Message });

        // Create HistorialPwd and set TienePasswordLocal=1
        var cambioResult = await _passwordService.CambiarPasswordAsync(
            id, _tenantContext.CurrentId, hashResult.Value, pepperVersion: 1,
            idTipoCambio: (int)PassPlat.Dominio.Enums.ETipoCambioPwd.PrimerUso,
            idDisp: null, idIP: null, idAgente: null, ct);

        if (cambioResult.IsFailure)
            return BadRequest(new { codigo = cambioResult.Error!.Code, mensaje = cambioResult.Error.Message });

        await _uow.SaveChangesAsync(ct);

        await _emailQueue.EnqueueAsync(new EmailJob(
            EmailJobKind.PasswordLocalAdded,
            usuario.Email ?? "",
            usuario.Nombre ?? usuario.NomUsuario,
            new Dictionary<string, object?>(),
            _tenantContext.CurrentId,
            id));

        return Ok(new { mensaje = "Contraseña local agregada correctamente. Ahora puede iniciar sesión con usuario y contraseña." });
    }

    private static CBP.Security.Cryptography.Models.PoliticaPwd PoliticaPwdDtoToCbpPolicy(PoliticaPwdDto? dto)
    {
        if (dto is null)
            return DefaultPermissivePolicy();

        return new CBP.Security.Cryptography.Models.PoliticaPwd
        {
            Codigo = dto.Codigo,
            Nombre = dto.Nombre,
            LongMin = dto.LongMin,
            LongMax = dto.LongMax,
            ReqMayuscula = dto.ReqMayuscula,
            ReqMinuscula = dto.ReqMinuscula,
            ReqNumero = dto.ReqNumero,
            ReqEspecial = dto.ReqEspecial,
            CaracteresEspeciales = dto.CaracteresEspeciales,
            ProhSecuenciales = dto.ProhSecuenciales,
            ProhRepetitivos = dto.ProhRepetitivos,
            ProhPatrones = dto.ProhPatrones,
            ProhPwdComun = dto.ProhPwdComun,
            ProhInfoUsuario = dto.ProhInfoUsuario,
            VerificarBrechas = dto.VerificarBrechas,
            PermitirEspacios = dto.PermitirEspacios,
            DiasVigencia = dto.DiasVigencia,
            PwdRecordadas = dto.PwdRecordadas,
            MaxIntentos = dto.MaxIntentos,
            DurBloqueoMin = dto.DurBloqueoMin,
            Activa = true,
            Version = 1
        };
    }

    private static CBP.Security.Cryptography.Models.PoliticaPwd DefaultPermissivePolicy() => new()
    {
        Codigo = "DEFAULT",
        Nombre = "Default",
        LongMin = 1, LongMax = 128,
        ReqMayuscula = false, ReqMinuscula = false, ReqNumero = false, ReqEspecial = false,
        ProhSecuenciales = false, ProhRepetitivos = false, ProhPatrones = false,
        ProhPwdComun = false, ProhInfoUsuario = false, VerificarBrechas = false, PermitirEspacios = true,
        DiasVigencia = 90, PwdRecordadas = 0, MaxIntentos = 5, DurBloqueoMin = 30,
        Activa = true, Version = 1
    };

    private async Task<bool> EsMiembroTenantActualAsync(int idUsuario, CancellationToken ct)
    {
        try
        {
            var membresia = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, _tenantContext.CurrentId, ct);
            return membresia.IsSuccess && membresia.Value != null && membresia.Value.Activo;
        }
        catch
        {
            return false;
        }
    }

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

public class PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CambiarPwdAdminRequest
{
    [Required(AllowEmptyStrings = false)]
    public string NuevaPassword { get; init; } = string.Empty;
}

public class AgregarPasswordLocalRequest
{
    [Required(AllowEmptyStrings = false)]
    public string NuevaPassword { get; init; } = string.Empty;
}
