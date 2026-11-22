using System.ComponentModel.DataAnnotations;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;

namespace PassPlat.WebAPI.Controllers;

[AllowAnonymous]
[Route("api/auth/externo")]
public class ExternalAuthController : BaseApiController
{
    private readonly IExternalAuthService _externalAuthService;
    private readonly IExternalLoginProviderService _externalLoginProviderService;
    private readonly IProvIdenRepository _provIdenRepo;
    private readonly IConfProvIdenRepository _confProvIdenRepo;
    private readonly ICacheService _cache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ExternalAuthController> _logger;
    private readonly ITenantInitializer _tenantInitializer;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ILoginExternoCanjeService _canje;
    private readonly IAuthService _authService;

    public ExternalAuthController(
        IExternalAuthService externalAuthService,
        IExternalLoginProviderService externalLoginProviderService,
        IProvIdenRepository provIdenRepo,
        IConfProvIdenRepository confProvIdenRepo,
        ICacheService cache,
        IWebHostEnvironment env,
        ILogger<ExternalAuthController> logger,
        ITenantInitializer tenantInitializer,
        ITenantContext tenantContext,
        IUnitOfWorkAsync uow,
        ILoginExternoCanjeService canje,
        IAuthService authService)
    {
        _externalAuthService = externalAuthService;
        _externalLoginProviderService = externalLoginProviderService;
        _provIdenRepo = provIdenRepo;
        _confProvIdenRepo = confProvIdenRepo;
        _cache = cache;
        _env = env;
        _logger = logger;
        _tenantInitializer = tenantInitializer;
        _tenantContext = tenantContext;
        _uow = uow;
        _canje = canje;
        _authService = authService;
    }

    // S65: una excepción inesperada no se devuelve al cliente; queda en el log con el traceId.
    private const string MensajeErrorInterno = "Error interno. Intente nuevamente más tarde.";

    private IActionResult ErrorInterno(string codigo, Exception ex)
    {
        _logger.LogError(ex, "Error interno {Codigo} trace {TraceId}", codigo, HttpContext.TraceIdentifier);
        return StatusCode(500, new { codigo, mensaje = MensajeErrorInterno, traceId = HttpContext.TraceIdentifier });
    }
    [HttpPost("login")]
    public async Task<IActionResult> LoginExterno([FromBody] LoginExternoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _externalAuthService.LoginExternoAsync(
                request.IdTenant, request.IdApp, request.ProviderCode,
                request.AuthorizationCode, request.RedirectUri,
                request.IdDisp, request.IdIP, request.IdAgente,
                request.CodeVerifier, ct);

            if (result.IsFailure)
            {
                var (statusCode, codigo) = result.Error!.Code switch
                {
                    "PROVIDER_ERROR" or "PROVIDER_NOT_FOUND" or "PROVIDER_NOT_CONFIGURED"
                        or "PROVIDER_DISABLED" or "PROVIDER_CONFIG_DISABLED" or "PROVIDER_INCOMPLETE_CONFIG"
                        or "SECRET_DECRYPT_ERROR" or "REPLAY_DETECTED" => (502, "provider_error"),
                    "OAuthUserWithoutEmail" => (400, "sin_email"),
                    "OAuthProviderDisabled" or "OAuthIdentityRevoked" => (403, "proveedor_inhabilitado"),
                    "OAuthAutoLinkDenied" or "OAuthProviderError" => (401, "auth_fallida"),
                    "OAuthRoleDefaultNotConfigured" => (400, "configuracion_incompleta"),
                    "CuentaInactiva" or "SinAccesoApp" => (403, "acceso_denegado"),
                    _ => (401, "error")
                };
                return StatusCode(statusCode, new { codigo, mensaje = result.Error.Message });
            }

            if (result.IsSuccess)
                await _uow.SaveChangesAsync(ct);

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
            return ErrorInterno("EXTERNAL_AUTH_ERROR", ex);
        }
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OAuthProvider"] = provider,
            ["OAuthState"] = state?[..Math.Min(state.Length, 12)] ?? "(null)",
            ["OAuthHasCode"] = !string.IsNullOrWhiteSpace(code),
            ["TraceId"] = HttpContext.TraceIdentifier
        });

        _logger.LogInformation(OAuthEventIds.CallbackStarted,
            "[1] Callback iniciado | Provider={Provider} | HasCode={HasCode} | HasState={HasState} | HasError={HasError}",
            provider, !string.IsNullOrWhiteSpace(code), !string.IsNullOrWhiteSpace(state), !string.IsNullOrWhiteSpace(error));

        // S128 · D4 — si la sesión OAuth identifica la App, el error vuelve a su UrlRetorno (ver UrlError).
        OAuthSession? session = null;

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning(OAuthEventIds.CallbackRedirect,
                "Redirect por error del proveedor: {Error}", error);
            if (!string.IsNullOrEmpty(state))
            {
                try
                {
                    var stateKeyError = $"oauth_state:{state}";
                    session = await _cache.GetAsync<OAuthSession>(stateKeyError, ct);
                    if (session != null)
                        await _cache.RemoveAsync(stateKeyError, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(OAuthEventIds.CallbackError, ex,
                        "No se pudo leer la sesión OAuth del error del proveedor");
                    session = null;
                }
            }
            return Redirect(UrlError(session, "proveedor_rechazo"));
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning(OAuthEventIds.CallbackError,
                "Callback sin código de autorización");
            return BadRequest(new { codigo = "NO_CODE", mensaje = "No se recibió código de autorización" });
        }

        try
        {
            if (string.IsNullOrEmpty(state))
            {
                _logger.LogWarning(OAuthEventIds.CallbackRedirect,
                    "Redirect por state nulo/vacío");
                return Redirect($"{_blazorBaseUrl}/login?error=state_invalido");
            }

            _logger.LogInformation(OAuthEventIds.CallbackSessionFound,
                "[2] Buscando OAuthSession | StateKey=oauth_state:{StatePrefix}", state[..Math.Min(state.Length, 12)]);

            var stateKey = $"oauth_state:{state}";
            session = await _cache.GetAsync<OAuthSession>(stateKey, ct);
            if (session == null)
            {
                _logger.LogWarning(OAuthEventIds.CallbackSessionMiss,
                    "[3] OAuthSession NO encontrada en cache | StateKey=oauth_state:{StatePrefix}", state[..Math.Min(state.Length, 12)]);
                return Redirect($"{_blazorBaseUrl}/login?error=state_invalido_o_expirado");
            }

            _logger.LogInformation(OAuthEventIds.CallbackSessionFound,
                "[3] OAuthSession encontrada | ProviderCode={ProviderCode} | IdTenant={IdTenant} | HasCodeVerifier={HasCodeVerifier}",
                session.ProviderCode, session.IdTenant, !string.IsNullOrWhiteSpace(session.CodeVerifier));

            await _cache.RemoveAsync(stateKey, ct);

            var providerUpper = provider.ToUpperInvariant();
            if (session.ProviderCode != providerUpper)
            {
                _logger.LogWarning(OAuthEventIds.CallbackRedirect,
                    "Redirect por proveedor no coincide | Session={SessionProvider} | Request={RequestProvider}",
                    session.ProviderCode, providerUpper);
                return Redirect(UrlError(session, "proveedor_no_coincide"));
            }

            if (string.IsNullOrEmpty(session.RedirectUri))
            {
                _logger.LogWarning(OAuthEventIds.CallbackRedirect,
                    "Redirect por RedirectUri nulo en sesión");
                return Redirect(UrlError(session, "redirect_uri_no_configurado"));
            }

            _logger.LogInformation(OAuthEventIds.CallbackLoginResult,
                "[4] Invocando LoginExternoAsync | RedirectUri={RedirectUri}", session.RedirectUri);

            var result = await _externalAuthService.LoginExternoAsync(
                session.IdTenant, session.IdApp, providerUpper, code, session.RedirectUri,
                null, null, null, session.CodeVerifier, ct);

            _logger.LogInformation(OAuthEventIds.CallbackLoginResult,
                "[5] LoginExternoAsync completado | IsSuccess={IsSuccess} | ErrorCode={ErrorCode}",
                result.IsSuccess, result.IsFailure ? result.Error?.Code : "(none)");

            if (result.IsSuccess)
                await _uow.SaveChangesAsync(ct);

            if (result.IsFailure)
            {
                if (_env.IsDevelopment())
                {
                    var errorCode = result.Error?.Code ?? "UNKNOWN";
                    var errorMsg = result.Error?.Message ?? "Error desconocido";
                    _logger.LogWarning(OAuthEventIds.CallbackError,
                        "Devolviendo BadRequest Development | Code={ErrorCode} | Message={ErrorMsg}", errorCode, errorMsg);
                    return BadRequest(new { codigo = errorCode, mensaje = errorMsg, provider });
                }
                _logger.LogWarning(OAuthEventIds.CallbackRedirect,
                    "Redirect login con error | Code={ErrorCode}", result.Error?.Code);
                return Redirect(UrlError(session, result.Error?.Message ?? "error"));
            }

            var response = result.Value;

            if (response.IdMFAPrincipal.HasValue)
            {
                _logger.LogInformation(OAuthEventIds.CallbackRedirect,
                    "[6] Redirect MFA | IdUsuario={IdUsuario} | IdMFAPrincipal={IdMFA}", response.IdUsuario, response.IdMFAPrincipal);
                // S125 · D4 — el MFA se completa en la Web PassPlat con una referencia opaca (sin ids en la URL).
                var tipoMfa = await _authService.PrepararMfaLoginAsync(response.IdUsuario, response.IdTenant, session.IdApp, ct);
                if (tipoMfa.IsFailure)
                    return Redirect(UrlError(session, "mfa_no_disponible"));

                var mfaRef = await _canje.EmitirMfaPendienteAsync(new MfaPendienteExterno(
                    response.IdUsuario, response.IdTenant, session.IdApp, response.IdMFAPrincipal.Value,
                    tipoMfa.Value, session.UrlRetorno ?? $"{_blazorBaseUrl}/signin-callback"), ct);
                return Redirect($"{_blazorBaseUrl}/login?mfaRef={Uri.EscapeDataString(mfaRef)}");
            }

            _logger.LogInformation(OAuthEventIds.CallbackRedirect,
                "[6] Redirect signin-callback | IdUsuario={IdUsuario} | IdTenant={IdTenant} | HasAccessToken={HasAT}",
                response.IdUsuario, response.IdTenant, !string.IsNullOrWhiteSpace(response.AccessToken));

            // S125 · D2 — sin tokens en la URL: código de un solo uso que la app canjea en api/auth/externo/canje.
            var codigoCanje = await _canje.EmitirCodigoAsync(response, session.IdApp, ct);
            return Redirect(LoginExternoCanjeService.AgregarCodigo(session.UrlRetorno ?? $"{_blazorBaseUrl}/signin-callback", codigoCanje));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(OAuthEventIds.CallbackError, ex,
                "Excepción en callback OAuth | Provider={Provider}", provider);
            // S65.1: código fijo; el texto de la excepción no viaja en la URL.
            return Redirect(UrlError(session, "error_interno"));
        }
    }

    [HttpGet("proveedores-login")]
    // S126 · D5 — idApp opcional (1 = PASSPLAT, como en authorize)
    public async Task<IActionResult> ObtenerProveedoresLogin([FromQuery] int idTenant, [FromQuery] int idApp = 1, CancellationToken ct = default)
    {
        if (idTenant <= 0)
            return Ok(Array.Empty<ExternalLoginProviderDto>());

        var result = await _externalLoginProviderService.ObtenerDisponiblesAsync(idTenant, idApp, ct);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var count = result.IsSuccess ? result.Value!.Count : 0;
            _logger.LogInformation("Proveedores-login | TenantId={IdTenant} | AppId={IdApp} | ConfiguracionesValidas={Count} | Proveedores={Providers}",
                idTenant, idApp, count, result.IsSuccess ? string.Join(",", result.Value!.Select(p => p.Codigo)) : "(error)");
        }

        return Ok(result.IsSuccess ? result.Value! : Array.Empty<ExternalLoginProviderDto>());
    }

    [HttpGet("{provider}/authorize")]
    public async Task<IActionResult> Authorize(string provider, [FromQuery] int idApp = 1, [FromQuery] int? idTenant = null, [FromQuery] string? urlRetorno = null, CancellationToken ct = default)
    {
        try
        {
            var tenantCode = HttpContext.Request.Headers["X-Tenant-Code"].FirstOrDefault();

            int resolvedIdTenant;
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                var initResult = await _tenantInitializer.InitializeAsync(tenantCode, ct);
                if (initResult.IsFailure)
                    return BadRequest(new { codigo = initResult.Error!.Code, mensaje = initResult.Error.Message });
                resolvedIdTenant = _tenantContext.CurrentId;
            }
            else if (idTenant.HasValue && idTenant.Value > 0)
            {
                resolvedIdTenant = idTenant.Value;
            }
            else
            {
                return BadRequest(new { codigo = "NO_TENANT", mensaje = "Se requiere X-Tenant-Code header o idTenant query param" });
            }

            // S125 · D1 — solo se vuelve a la base de la App (Apps.UrlBase; sin ella, la Web PassPlat).
            var retorno = await _canje.ResolverUrlRetornoAsync(idApp, urlRetorno, _blazorBaseUrl, ct);
            if (retorno.IsFailure)
                return BadRequest(new { codigo = retorno.Error!.Code, mensaje = retorno.Error.Message });

            var result = await _externalAuthService.GenerateAuthorizationUrlAsync(provider.ToUpperInvariant(), resolvedIdTenant, idApp, retorno.Value, ct);
            if (result.IsFailure)
                return BadRequest(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(new { authorizationUrl = result.Value });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("AUTHORIZE_ERROR", ex);
        }
    }

    /// <summary>S125 · D3 — canjea el código de un solo uso del login externo por la sesión.</summary>
    [HttpPost("canje")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Canjear([FromBody] CanjeLoginExternoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _canje.CanjearAsync(request.Codigo, request.IdApp, ct);
            if (result.IsFailure)
                return BadRequest(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(result.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("CANJE_ERROR", ex);
        }
    }

    /// <summary>S125 · D4 — tipo de MFA de un login externo pendiente (la Web muestra el panel).</summary>
    [HttpGet("mfa/{mfaRef}")]
    public async Task<IActionResult> ObtenerMfaPendiente(string mfaRef, CancellationToken ct)
    {
        try
        {
            var result = await _canje.ObtenerTipoMfaPendienteAsync(mfaRef, ct);
            if (result.IsFailure)
                return BadRequest(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(new { idTipoMFA = result.Value ?? 0 });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("MFA_PENDIENTE_ERROR", ex);
        }
    }

    /// <summary>S125 · D4 — valida el MFA del login externo y devuelve la URL de retorno con el código de canje.</summary>
    [HttpPost("mfa/validar")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> ValidarMfa([FromBody] ValidarMfaExternoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _canje.ValidarMfaAsync(request.MfaRef, request.CodigoMFA, ct);

            await _uow.SaveChangesAsync(ct);

            if (result.IsFailure)
                return BadRequest(new { codigo = result.Error!.Code, mensaje = result.Error.Message });

            return Ok(new { urlRetorno = result.Value });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorInterno("MFA_VALIDAR_ERROR", ex);
        }
    }

    [HttpGet("proveedores")]
    public async Task<IActionResult> ObtenerProveedores(CancellationToken ct, [FromQuery] int idTenant = 1)
    {
        var provsResult = await _provIdenRepo.GetAllAsync(limit: 50, asNoTracking: true, ct);
        if (provsResult.IsFailure)
            return Ok(new[]
            {
                new { codigo = "GOOGLE", nombre = "Google", icono = "google", orden = 0, activo = true, logo = (string?)null, color = (string?)null, tooltip = (string?)null },
                new { codigo = "GITHUB", nombre = "GitHub", icono = "github", orden = 0, activo = true, logo = (string?)null, color = (string?)null, tooltip = (string?)null },
                new { codigo = "INSTAGRAM", nombre = "Instagram", icono = "camera_alt", orden = 0, activo = true, logo = (string?)null, color = (string?)null, tooltip = (string?)null },
                new { codigo = "FACEBOOK", nombre = "Facebook", icono = "facebook", orden = 0, activo = true, logo = (string?)null, color = (string?)null, tooltip = (string?)null }
            });

        var confResult = await _confProvIdenRepo.WhereAsync(c => c.IdTenant == idTenant, asNoTracking: true, ct);
        var confs = confResult.IsSuccess ? confResult.Value!.ToDictionary(c => c.IdProvIden) : [];

        var providers = provsResult.Value
            .Select(p =>
            {
                confs.TryGetValue(p.Id, out var conf);
                return new
                {
                    codigo = p.Codigo,
                    nombre = conf?.Tooltip ?? p.Nombre,
                    icono = p.Icono ?? p.Codigo.ToLowerInvariant(),
                    tipoProveedor = p.TipoProveedor,
                    activo = p.Activo && (conf?.Activo ?? true),
                    orden = conf?.OrdenVisual ?? (int)p.Orden,
                    logo = conf?.Logo,
                    color = conf?.Color,
                    tooltip = conf?.Tooltip
                };
            })
            .OrderBy(p => p.orden)
            .ThenBy(p => p.codigo)
            .ToList();

        return Ok(providers);
    }

    private string _blazorBaseUrl =>
        Environment.GetEnvironmentVariable("BLAZOR_BASE_URL")
        ?? (HttpContext.Request.IsHttps ? "https://localhost:7275" : "http://localhost:5273");

    /// <summary>
    /// S128 · D4 — destino de un error del callback: la UrlRetorno de la App (validada en authorize contra
    /// Apps.UrlBase) con <c>error=</c>; sin sesión que identifique la App, el /login de la Web PassPlat.
    /// </summary>
    private string UrlError(OAuthSession? sesion, string codigo)
    {
        var valor = Uri.EscapeDataString(codigo);
        var retorno = sesion?.UrlRetorno;
        if (string.IsNullOrWhiteSpace(retorno))
            return $"{_blazorBaseUrl}/login?error={valor}";
        return $"{retorno}{(retorno.Contains('?') ? '&' : '?')}error={valor}";
    }
}

public class LoginExternoRequest
{
    [Required] public int IdTenant { get; init; }
    [Required] public int IdApp { get; init; }
    [Required] public string ProviderCode { get; init; } = string.Empty;
    [Required] public string AuthorizationCode { get; init; } = string.Empty;
    [Required] public string RedirectUri { get; init; } = string.Empty;
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
    public string? CodeVerifier { get; init; }
}

/// <summary>S125 · D3 — canje del código de un solo uso.</summary>
public class CanjeLoginExternoRequest
{
    [Required] public string Codigo { get; init; } = string.Empty;
    [Required] public int IdApp { get; init; }
}

/// <summary>S125 · D4 — código MFA de un login externo pendiente.</summary>
public class ValidarMfaExternoRequest
{
    [Required] public string MfaRef { get; init; } = string.Empty;
    [Required] public string CodigoMFA { get; init; } = string.Empty;
}
