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
using CBP.Data.Utilities.Helpers;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.WebAPI.Auth;

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
    private readonly IUsuarioPermisoService _usuarioPermiso;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(IUsuarioService service, ITenantContext tenantContext, IPasswordService passwordService, IPoliticaPwdService politicaPwdService, IUnitOfWorkAsync uow, IValidator<CrearUsuarioDto> crearValidator, IEmailQueue emailQueue, IUsuarioTenantRepository usuarioTenantRepo, ILogger<UsuariosController> logger, IUsuarioPermisoService usuarioPermiso)
    {
        _service = service;
        _tenantContext = tenantContext;
        _passwordService = passwordService;
        _politicaPwdService = politicaPwdService;
        _uow = uow;
        _crearValidator = crearValidator;
        _emailQueue = emailQueue;
        _usuarioTenantRepo = usuarioTenantRepo;
        _logger = logger;
        _usuarioPermiso = usuarioPermiso;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? idApp)
    {
        if (idApp.HasValue)
        {
            var denegado = await ValidarAppAsync(idApp.Value, ct);
            if (denegado is not null) return denegado;
            var pagedResult = await _service.ObtenerPaginadoFiltradoAsync(User.AlcanceTenant(_tenantContext), idApp.Value, null, null, new PaginationOptions<Usuario>(1, 200), ct);
            if (pagedResult.IsFailure) return FromResult(pagedResult);
            return Ok(pagedResult.Value.Items);
        }
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
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, [FromQuery] int? idTenant, [FromQuery] int? idApp, [FromQuery] int? miembroDe, CancellationToken ct)
    {
        // S111 · D4 — con idApp se exige Acceso activo a la app en el tenant (403 SIN_ACCESO_APP)
        if (idApp is int app && await ValidarAppAsync(app, ct) is { } denegado)
            return denegado;

        var options = new PaginationOptions<Usuario>(request.PageNumber, request.PageSize);
        // S85 · D5 — un no-sistema nunca ve otro tenant, aunque mande idTenant
        // S93 · D2 — miembroDe filtra por membresía activa en lugar del tenant de origen; un no-sistema queda en el suyo
        var esSistema = User.HasClaim("is_system", "true");
        int? miembro = miembroDe.HasValue ? (esSistema ? miembroDe : _tenantContext.CurrentId) : null;
        int? tenant = miembro.HasValue ? null : (esSistema ? idTenant : _tenantContext.CurrentId);
        var result = await _service.ObtenerPaginadoFiltradoAsync(tenant, idApp, miembro, search, options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpGet("nomusuario/{nomUsuario}")]
    public async Task<IActionResult> GetByNomUsuario(string nomUsuario, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerPorNomUsuarioAsync(_tenantContext.CurrentId, nomUsuario, ct));
    }

    [HttpGet("email/{email}")]
    public async Task<IActionResult> GetByEmail(string email, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerPorEmailAsync(_tenantContext.CurrentId, email, ct));
    }

    [HttpPost]
    [Authorize(Policy = "USUARIOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearUsuarioDto dto, CancellationToken ct)
    {
        var uid = User.IdUsuario();
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

    // S111 · D4 — valida que quien llama tenga Acceso activo a la app en su tenant (403 SIN_ACCESO_APP)
    private async Task<IActionResult?> ValidarAppAsync(int idApp, CancellationToken ct)
    {
        var apps = await _usuarioPermiso.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, User.AlcanceTenant(_tenantContext), idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        return null;
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
            return RestriccionBd("actualizar", ex);
        }
        return FromResult(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "USUARIOS_ELIMINAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var uid = User.IdUsuario();
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
            return RestriccionBd("eliminar", ex);
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

        var idUsrEjecutor = User.IdUsuario();
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

    // S65.1: el texto de la excepción (SQL, índice, valor duplicado) no se devuelve al cliente.
    private IActionResult RestriccionBd(string accion, DbUpdateException ex)
    {
        var numero = SqlErrorRedactor.GetErrorNumber(ex);
        var innerEx = ex.InnerException;
        var innerMessage = innerEx?.Message;
        var detalle = SqlErrorRedactor.Redact(innerMessage);
        _logger.LogWarning("Restricción de BD en usuarios {Accion} trace {TraceId} {Numero}: {Detalle}", accion, HttpContext.TraceIdentifier, numero, detalle);
        var mensaje = EsViolacionIndiceUnico(ex)
            ? "Ya existe un usuario con ese nombre de usuario o email."
            : "No se pudo guardar el usuario: los datos no cumplen una restricción de la base de datos.";
        return BadRequest(new { codigo = "DB_CONSTRAINT", mensaje });
    }

    private static bool EsViolacionIndiceUnico(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            if (e is Microsoft.Data.SqlClient.SqlException sqlex &&
                (sqlex.Number == 2601 || sqlex.Number == 2627))
                return true;
        }
        return false;
    }
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
