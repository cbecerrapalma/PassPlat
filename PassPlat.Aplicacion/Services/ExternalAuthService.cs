using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using CBP.Caching.Interfaces;
using CBP.Caching.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Aplicacion.Services.OAuth;
using PassPlat.Dominio.Enums;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

internal class ProveedorTokenMetadata
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public string? IdToken { get; set; }
}

public interface IExternalAuthService
{
    Task<Result<AuthResponseDto>> LoginExternoAsync(int idTenant, int idApp, string providerCode, string authorizationCode, string redirectUri, int? idDisp = null, int? idIP = null, int? idAgente = null, string? codeVerifier = null, string? nonce = null, CancellationToken ct = default);
    Task<Result<string>> GenerateAuthorizationUrlAsync(string providerCode, int idTenant, int idApp = 1, CancellationToken ct = default);
}

public class ExternalAuthService : IExternalAuthService
{
    private readonly IEnumerable<IExternalIdentityProvider> _providers;
    private readonly IExternalAuthRepository _externalAuthRepo;
    private readonly IConfProvIdenRepository _confProvIdenRepo;
    private readonly IProvIdenRepository _provIdenRepo;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<ExternalAuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailQueue _emailQueue;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IIdenExtRepository _idenExtRepo;
    private readonly IIdenExtTokensRepository _idenExtTokensRepo;
    private readonly ICacheService _cache;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;

    public ExternalAuthService(
        IEnumerable<IExternalIdentityProvider> providers,
        IExternalAuthRepository externalAuthRepo,
        IConfProvIdenRepository confProvIdenRepo,
        IProvIdenRepository provIdenRepo,
        IEncryptionService encryption,
        ILogger<ExternalAuthService> logger,
        IHttpContextAccessor httpContextAccessor,
        IEmailQueue emailQueue,
        IUsuarioRepository usuarioRepo,
        IIdenExtRepository idenExtRepo,
        IIdenExtTokensRepository idenExtTokensRepo,
        ICacheService cache,
        IAuthenticationTokenService tokenService,
        IUsuarioTenantRepository usuarioTenantRepo)
    {
        _providers = providers;
        _externalAuthRepo = externalAuthRepo;
        _confProvIdenRepo = confProvIdenRepo;
        _provIdenRepo = provIdenRepo;
        _encryption = encryption;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _emailQueue = emailQueue;
        _usuarioRepo = usuarioRepo;
        _idenExtRepo = idenExtRepo;
        _idenExtTokensRepo = idenExtTokensRepo;
        _cache = cache;
        _tokenService = tokenService;
        _usuarioTenantRepo = usuarioTenantRepo;
    }

    public async Task<Result<AuthResponseDto>> LoginExternoAsync(int idTenant, int idApp, string providerCode, string authorizationCode, string redirectUri, int? idDisp = null, int? idIP = null, int? idAgente = null, string? codeVerifier = null, string? nonce = null, CancellationToken ct = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OAuthProvider"] = providerCode,
            ["OAuthIdTenant"] = idTenant,
            ["OAuthIdApp"] = idApp,
            ["TraceId"] = _httpContextAccessor.HttpContext?.TraceIdentifier ?? "(none)"
        });

        _logger.LogInformation(OAuthEventIds.LoginStarted,
            "[1] LoginExternoAsync iniciado | Provider={Provider} | IdTenant={IdTenant} | IdApp={IdApp} | HasCodeVerifier={HasCV} | HasNonce={HasNonce}",
            providerCode, idTenant, idApp, !string.IsNullOrWhiteSpace(codeVerifier), !string.IsNullOrWhiteSpace(nonce));

        var provider = _providers.FirstOrDefault(p => p.ProviderCode == providerCode);
        if (provider == null)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Provider no encontrado en DI: {ProviderCode}", providerCode);
            return Result<AuthResponseDto>.Failure("PROVIDER_NOT_FOUND", $"Proveedor '{providerCode}' no encontrado");
        }

        _logger.LogInformation(OAuthEventIds.LoginConfigOk, "[2] Provider encontrado en DI");
        var provIdenResult = await _provIdenRepo.ObtenerPorCodigoAsync(providerCode, ct);
        if (provIdenResult.IsFailure || provIdenResult.Value == null)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Provider no encontrado en catálogo: {ProviderCode}", providerCode);
            return Result<AuthResponseDto>.Failure("PROVIDER_NOT_FOUND", $"Proveedor '{providerCode}' no encontrado en catálogo");
        }

        var provIden = provIdenResult.Value;
        if (!provIden.Activo)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Provider deshabilitado en catálogo: {ProviderCode}", providerCode);
            return Result<AuthResponseDto>.Failure("PROVIDER_DISABLED", $"El proveedor '{providerCode}' está deshabilitado en el catálogo");
        }

        _logger.LogInformation(OAuthEventIds.LoginConfigOk, "[3] Catálogo OK | IdProvIden={IdProvIden}", provIden.Id);
        var confResult = await _confProvIdenRepo.ObtenerConfiguracionAsync(idTenant, provIden.Id, ct);
        if (confResult.IsFailure || confResult.Value == null)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Configuración no encontrada para tenant {IdTenant} provider {IdProvIden}", idTenant, provIden.Id);
            return Result<AuthResponseDto>.Failure("PROVIDER_NOT_CONFIGURED", "Proveedor no configurado para este tenant");
        }

        var conf = confResult.Value;
        if (!conf.Activo)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Configuración deshabilitada para tenant {IdTenant}", idTenant);
            return Result<AuthResponseDto>.Failure("PROVIDER_CONFIG_DISABLED", "La configuración del proveedor está deshabilitada para este tenant");
        }

        if (string.IsNullOrWhiteSpace(conf.ClientId))
            return Result<AuthResponseDto>.Failure("PROVIDER_INCOMPLETE_CONFIG", "ClientId no configurado");
        if (string.IsNullOrWhiteSpace(conf.ClientSecret))
            return Result<AuthResponseDto>.Failure("PROVIDER_INCOMPLETE_CONFIG", "ClientSecret no configurado");
        if (string.IsNullOrWhiteSpace(conf.Callback))
            return Result<AuthResponseDto>.Failure("PROVIDER_INCOMPLETE_CONFIG", "Callback URI no configurado");

        _logger.LogInformation(OAuthEventIds.LoginConfigOk, "[4] Config OK | ClientId={ClientId} | Callback={Callback} | Scopes={Scopes}",
            conf.ClientId[..Math.Min(conf.ClientId.Length, 20)], conf.Callback, conf.Scopes ?? "(default)");

        var codeKey = $"oauth_code:{authorizationCode}";
        _logger.LogInformation(OAuthEventIds.LoginReplayCheck, "[5] Verificando replay | CodeKey=oauth_code:{CodePrefix}", authorizationCode[..Math.Min(authorizationCode.Length, 12)]);

        var codeExists = await _cache.ExistsAsync(codeKey, ct);
        if (codeExists)
        {
            _logger.LogWarning(OAuthEventIds.LoginError, "Replay detectado para código: {CodePrefix}", authorizationCode[..Math.Min(authorizationCode.Length, 12)]);
            return Result<AuthResponseDto>.Failure("REPLAY_DETECTED", "El código de autorización ya ha sido utilizado");
        }

        await _cache.SetAsync(codeKey, true, new CacheEntryOptions(TimeSpan.FromMinutes(10)), ct);
        _logger.LogInformation(OAuthEventIds.LoginReplayCheck, "[5] Replay OK, código marcado como usado");

        _logger.LogInformation(OAuthEventIds.LoginSecretDecrypted, "[6] Descifrando ClientSecret...");
        string clientSecret;
        try
        {
            clientSecret = _encryption.Decrypt(conf.ClientSecret, "ConfProvIden");
            _logger.LogInformation(OAuthEventIds.LoginSecretDecrypted, "[6] ClientSecret descifrado OK | Longitud={Len}", clientSecret.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(OAuthEventIds.LoginError, ex, "[6] Error al descifrar ClientSecret");
            return Result<AuthResponseDto>.Failure("SECRET_DECRYPT_ERROR", $"Error al descifrar ClientSecret: {ex.Message}");
        }

        _logger.LogInformation(OAuthEventIds.LoginClaimsStarted, "[7] Llamando ValidateAndExtractClaimsAsync...");
        var claimsResult = await provider.ValidateAndExtractClaimsAsync(authorizationCode, redirectUri, conf.ClientId, clientSecret, conf.Scopes, codeVerifier, nonce, ct);
        if (claimsResult.IsFailure)
        {
            _logger.LogWarning(OAuthEventIds.LoginClaimsResult,
                "[7] ValidateAndExtractClaimsAsync FALLÓ | Code={ErrorCode} | Message={ErrorMsg}",
                claimsResult.Error?.Code, claimsResult.Error?.Message);
            return Result<AuthResponseDto>.Failure(claimsResult.Error!);
        }

        var claims = claimsResult.Value;
        _logger.LogInformation(OAuthEventIds.LoginClaimsResult,
            "[7] Claims obtenidos | Sub={Sub} | Email={Email} | Nombre={Nombre} | HasAvatar={HasAvatar}",
            claims.Sub, claims.Email ?? "(sin email)", claims.Nombre ?? "(sin nombre)", !string.IsNullOrWhiteSpace(claims.Avatar));

        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

        _logger.LogInformation(OAuthEventIds.LoginRepoResult, "[8] Llamando ExternalAuthRepo.LoginExternoAsync...");
        var loginResult = await _externalAuthRepo.LoginExternoAsync(
            idTenant, idApp, conf.IdProvIden, claims.Sub,
            claims.Email, claims.Nombre, claims.Avatar, claims.MetadataJson,
            ip, userAgent, idDisp, idAgente, ct);

        if (loginResult.IsFailure)
        {
            _logger.LogWarning(OAuthEventIds.LoginRepoResult,
                "[8] ExternalAuthRepo.LoginExternoAsync FALLÓ | Code={ErrorCode} | Message={ErrorMsg}",
                loginResult.Error?.Code, loginResult.Error?.Message);
            return Result<AuthResponseDto>.Failure(loginResult.Error!);
        }

        var result = loginResult.Value;
        _logger.LogInformation(OAuthEventIds.LoginRepoResult,
            "[8] LoginExternoAsync OK | Resultado={Resultado} | IdUsuario={IdUsuario} | IdMFAPrincipal={IdMFA} | ReqCambioPwd={ReqPwd}",
            result.Resultado, result.IdUsuario, result.IdMFAPrincipal, result.ReqCambioPwd);

        var traceId = _httpContextAccessor.HttpContext?.TraceIdentifier;
        var esExitoso = result.Resultado == (int)EResultadoAcceso.Exitoso
            || result.Resultado == (int)EResultadoAcceso.OAuthLogin
            || result.Resultado == (int)EResultadoAcceso.OAuthProvisioning
            || result.Resultado == (int)EResultadoAcceso.OAuthIdentityLinked;
        var resultadoAuditoria = result.IdMFAPrincipal.HasValue ? "MFA_REQUERIDO"
            : esExitoso ? "EXITOSO" : "FALLIDO";

        // Campos base disponibles antes de generar token/sesión (ETAPA 12)
        async Task RegistrarAuditoriaExtendidaAsync(string? jwtId = null, Guid? sessionId = null, int? httpStatus = null)
        {
            var auditResult = await _externalAuthRepo.RegistrarAuditoriaAsync(
                idTenant, conf.IdProvIden, result.IdUsuario, claims.Sub,
                "LOGIN_EXTERNO", resultadoAuditoria,
                result.Mensaje, ip, userAgent, null,
                traceId: traceId, sessionId: sessionId, jwtId: jwtId,
                httpStatus: httpStatus, scopes: conf.Scopes, metodoAutenticacion: "OAuth2",
                tipoLogin: providerCode, origen: redirectUri, destino: conf.Callback,
                idDevice: idDisp, browser: ExtraerBrowser(userAgent), os: ExtraerOS(userAgent), ct: ct);
            if (auditResult.IsFailure)
                _logger.LogWarning(OAuthEventIds.LoginAuditFailed, "No se registró auditoría LOGIN_EXTERNO | Error={Error}",
                    auditResult.Error?.Message);
        }

        if (result.Resultado == (int)EResultadoAcceso.MFARequerido)
        {
            _logger.LogInformation(OAuthEventIds.LoginCompleted, "MFA requerido para usuario {IdUsuario}", result.IdUsuario);
            await RegistrarAuditoriaExtendidaAsync(httpStatus: 200);
            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                IdMFAPrincipal = result.IdMFAPrincipal,
                IdUsuario = result.IdUsuario ?? 0,
                IdTenant = idTenant,
                NomUsuario = claims.Nombre ?? claims.Sub,
                Email = claims.Email ?? string.Empty
            });
        }

        if (!esExitoso)
        {
            var errorCode = result.Resultado switch
            {
                12 => "OAuthProviderDisabled",
                14 => "OAuthIdentityRevoked",
                15 => "OAuthProviderError",
                16 => "OAuthUserWithoutEmail",
                17 => "OAuthAutoLinkDenied",
                18 => "OAuthRoleDefaultNotConfigured",
                6 => "CuentaInactiva",
                4 => "SinAccesoApp",
                _ => "AUTH_FAILED"
            };

            _logger.LogWarning(OAuthEventIds.LoginError,
                "Login no exitoso | Resultado={Resultado} | ErrorCode={ErrorCode} | Mensaje={Mensaje}",
                result.Resultado, errorCode, result.Mensaje);

            if ((result.Resultado == 6 || result.Resultado == 4) && result.IdUsuario.HasValue)
                await NotificarErrorAuthAsync(result.IdUsuario.Value, idTenant, conf.IdProvIden, providerCode, result.Mensaje, ct);

            await RegistrarAuditoriaExtendidaAsync(httpStatus: 401);
            return Result<AuthResponseDto>.Failure(errorCode, result.Mensaje ?? "Error de autenticación externa");
        }

        // H2: Persistir tokens del proveedor OAuth (AT, RT, IT) en IdenExtTokens
        _logger.LogInformation(OAuthEventIds.LoginTokenGenerated, "[9] Persistiendo tokens del proveedor...");
        var persistenceResult = await PersistirTokensProveedorAsync(conf.IdProvIden, providerCode, claims, result.IdUsuario!.Value, idTenant, ct);
        if (persistenceResult.IsFailure)
        {
            var auditCorrelationId = Guid.NewGuid().ToString("N");
            await _externalAuthRepo.RegistrarAuditoriaAsync(
                idTenant, conf.IdProvIden, result.IdUsuario.Value, claims.Sub,
                "TOKEN_PERSIST_FAILED", "FALLIDO",
                $"Provider={providerCode}, Error={persistenceResult.Error?.Message}",
                correlationId: auditCorrelationId, ct: ct);
            _logger.LogWarning("No se pudieron persistir tokens del proveedor {Provider} para usuario {Usuario}: {Error}",
                providerCode, result.IdUsuario.Value, persistenceResult.Error?.Message);
        }

        _logger.LogInformation(OAuthEventIds.LoginTokenGenerated, "[10] Generando JWT interno y sesión...");
        var idUsuarioTenantResult = await _usuarioTenantRepo.ResolverIdUsuarioTenantAsync(result.IdUsuario!.Value, idTenant, ct);
        var idUsuarioTenant = idUsuarioTenantResult.IsSuccess ? (int?)idUsuarioTenantResult.Value : null;
        var authContext = new AuthenticationContext(
            result.IdUsuario!.Value, idTenant, idApp,
            IdDispositivo: null, IdIp: null,
            Origen: AuthenticationOrigin.OAuth,
            EsSistema: result.EsSistema,
            IdUsuarioTenant: idUsuarioTenant);
        var tokenResult = await _tokenService.OAuthAsync(authContext, ct);
        if (tokenResult.IsFailure)
            return Result<AuthResponseDto>.Failure(tokenResult.Error!);

        var sessionId = tokenResult.Value.SessionId;
        _logger.LogInformation(OAuthEventIds.LoginSessionCreated, "[11] Sesión creada | SessionId={SessionId}", sessionId);

        await RegistrarAuditoriaExtendidaAsync(jwtId: tokenResult.Value.Jti, sessionId: sessionId, httpStatus: 200);

        await NotificarLoginExternoAsync(result.IdUsuario.Value, idTenant, conf.IdProvIden, providerCode, claims.Nombre, ct);

        if (result.Resultado == (int)EResultadoAcceso.OAuthIdentityLinked ||
            result.Resultado == (int)EResultadoAcceso.OAuthProvisioning)
        {
            await NotificarVinculacionExternoAsync(result.IdUsuario.Value, idTenant, conf.IdProvIden, providerCode, ct);
        }

        _logger.LogInformation(OAuthEventIds.LoginCompleted,
            "[12] Login completado | IdUsuario={IdUsuario} | IdTenant={IdTenant}",
            result.IdUsuario.Value, idTenant);

        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            AccessToken = tokenResult.Value.AccessToken,
            RefreshToken = tokenResult.Value.RefreshToken,
            ExpiresAt = tokenResult.Value.ExpiresAt,
            IdUsuario = result.IdUsuario.Value,
            IdTenant = idTenant,
            NomUsuario = claims.Nombre ?? claims.Sub,
            Email = claims.Email ?? string.Empty,
            Nombre = claims.Nombre ?? string.Empty,
            ReqCambioPwd = result.ReqCambioPwd ?? false
        });
    }

    public async Task<Result<string>> GenerateAuthorizationUrlAsync(string providerCode, int idTenant, int idApp = 1, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderCode == providerCode);
        if (provider == null)
            return Result<string>.Failure("PROVIDER_NOT_FOUND", $"Proveedor '{providerCode}' no encontrado");

        var provIdenResult = await _provIdenRepo.ObtenerPorCodigoAsync(providerCode, ct);
        if (provIdenResult.IsFailure || provIdenResult.Value == null)
            return Result<string>.Failure("PROVIDER_NOT_FOUND", $"Proveedor '{providerCode}' no encontrado en catálogo");

        var provIden = provIdenResult.Value;
        if (!provIden.Activo)
            return Result<string>.Failure("PROVIDER_DISABLED", $"El proveedor '{providerCode}' está deshabilitado en el catálogo");

        var confResult = await _confProvIdenRepo.ObtenerConfiguracionAsync(idTenant, provIden.Id, ct);
        if (confResult.IsFailure || confResult.Value == null)
            return Result<string>.Failure("PROVIDER_NOT_CONFIGURED", "Proveedor no configurado para este tenant");

        var conf = confResult.Value;
        if (!conf.Activo)
            return Result<string>.Failure("PROVIDER_CONFIG_DISABLED", "La configuración del proveedor está deshabilitada para este tenant");

        if (string.IsNullOrWhiteSpace(conf.ClientId))
            return Result<string>.Failure("PROVIDER_INCOMPLETE_CONFIG", "ClientId no configurado");
        if (string.IsNullOrWhiteSpace(conf.ClientSecret))
            return Result<string>.Failure("PROVIDER_INCOMPLETE_CONFIG", "ClientSecret no configurado");
        if (string.IsNullOrWhiteSpace(conf.Callback))
            return Result<string>.Failure("PROVIDER_INCOMPLETE_CONFIG", "Callback URI no configurado");

        var state = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))).ToLowerInvariant();

        var codeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var codeChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        await _cache.SetAsync($"oauth_state:{state}", new OAuthSession
        {
            CodeVerifier = codeVerifier,
            Nonce = nonce,
            ProviderCode = providerCode,
            IdTenant = idTenant,
            IdApp = idApp,
            RedirectUri = conf.Callback
        }, new CacheEntryOptions(TimeSpan.FromMinutes(10)), ct);

        return await provider.GenerateAuthorizationUrlAsync(conf.Callback, conf.ClientId, conf.Scopes, state, codeChallenge, nonce, ct);
    }

    private async Task NotificarLoginExternoAsync(int idUsuario, int idTenant, int idProvIden, string providerCode, string? nombreExterno, CancellationToken ct)
    {
        try
        {
            var userResult = await _usuarioRepo.GetByIdAsync(idUsuario, ct);
            if (userResult.IsFailure || userResult.Value == null) return;
            var user = userResult.Value;
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.ExternalLogin,
                user.Email,
                user.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["ProviderCode"] = providerCode,
                    ["ProviderName"] = providerCode,
                    ["IdProvIden"] = idProvIden,
                    ["NombreExterno"] = nombreExterno ?? user.NomUsuario
                },
                IdTenant: idTenant,
                IdUsuario: idUsuario), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al encolar notificación de login externo para usuario {IdUsuario}", idUsuario);
        }
    }

    private async Task NotificarVinculacionExternoAsync(int idUsuario, int idTenant, int idProvIden, string providerCode, CancellationToken ct)
    {
        try
        {
            var userResult = await _usuarioRepo.GetByIdAsync(idUsuario, ct);
            if (userResult.IsFailure || userResult.Value == null) return;
            var user = userResult.Value;
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.ExternalIdentityLinked,
                user.Email,
                user.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["ProviderCode"] = providerCode,
                    ["ProviderName"] = providerCode,
                    ["IdProvIden"] = idProvIden
                },
                IdTenant: idTenant,
                IdUsuario: idUsuario), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al encolar notificación de vinculación externa para usuario {IdUsuario}", idUsuario);
        }
    }

    private async Task NotificarErrorAuthAsync(int idUsuario, int idTenant, int idProvIden, string providerCode, string? mensajeError, CancellationToken ct)
    {
        try
        {
            var userResult = await _usuarioRepo.GetByIdAsync(idUsuario, ct);
            if (userResult.IsFailure || userResult.Value == null) return;
            var user = userResult.Value;
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.AuthError,
                user.Email,
                user.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["ProviderCode"] = providerCode,
                    ["ProviderName"] = providerCode,
                    ["IdProvIden"] = idProvIden,
                    ["Error"] = mensajeError
                },
                IdTenant: idTenant,
                IdUsuario: idUsuario), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al encolar notificación de error auth externo para usuario {IdUsuario}", idUsuario);
        }
    }

    private async Task<Result> PersistirTokensProveedorAsync(int idProvIden, string providerCode, ExternalIdentityClaims claims, int idUsuario, int idTenant, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(claims.MetadataJson))
                return Result.Success();

            var idenExtResult = await _idenExtRepo.ObtenerPorSubExternoAsync(idProvIden, claims.Sub, ct);
            if (idenExtResult.IsFailure || idenExtResult.Value == null)
                return Result.Failure("IDENEXT_NOT_FOUND", "Identidad externa no encontrada tras login");

            var idenExt = idenExtResult.Value;
            var metadata = System.Text.Json.JsonSerializer.Deserialize<ProveedorTokenMetadata>(claims.MetadataJson);
            if (metadata == null)
                return Result.Success();

            var correlationId = Guid.NewGuid().ToString("N");
            var now = DateTime.Now;

            byte[]? EncryptToken(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var encrypted = _encryption.Encrypt(raw, "IdenExtTokens");
                return System.Text.Encoding.UTF8.GetBytes(encrypted);
            }

            string? HashToken(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            }

            var accessTokenEnc = EncryptToken(metadata.AccessToken);
            var refreshTokenEnc = EncryptToken(metadata.RefreshToken);
            var idTokenEnc = EncryptToken(metadata.IdToken);

            var accessTokenHash = HashToken(metadata.AccessToken);
            var refreshTokenHash = HashToken(metadata.RefreshToken);
            var idTokenHash = HashToken(metadata.IdToken);

            var token = IdenExtTokens.Crear(
                idIdenExt: idenExt.Id,
                accessTokenEnc: accessTokenEnc,
                accessTokenHash: accessTokenHash,
                accessTokenExpires: metadata.ExpiresIn.HasValue ? now.AddSeconds(metadata.ExpiresIn.Value) : null,
                refreshTokenEnc: refreshTokenEnc,
                refreshTokenHash: refreshTokenHash,
                refreshTokenExpires: metadata.ExpiresIn.HasValue ? now.AddDays(90) : null,
                idTokenEnc: idTokenEnc,
                idTokenHash: idTokenHash,
                scope: claims.Scope,
                tokenType: "Bearer",
                correlationId: correlationId,
                hashAlgoritmo: "SHA256");

            var addResult = _idenExtTokensRepo.Add(token);
            if (addResult.IsFailure)
                return addResult;

            await _idenExtTokensRepo.MarcarTokenAnteriorInactivoAsync(idenExt.Id, token.Version, ct);

            await _externalAuthRepo.RegistrarAuditoriaAsync(
                idTenant, idProvIden, idUsuario, claims.Sub,
                "REFRESH_TOKEN_GUARDADO", "EXITOSO",
                correlationId: correlationId, ct: ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al persistir tokens del proveedor {Provider}", providerCode);
            return Result.Failure("TOKEN_PERSIST_ERROR", ex.Message);
        }
    }

    // ETAPA 12: Extracción ligera de Browser/OS desde User-Agent para auditoría
    private static string? ExtraerBrowser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("Edg/")) return "Edge";
        if (userAgent.Contains("Chrome/")) return "Chrome";
        if (userAgent.Contains("Firefox/")) return "Firefox";
        if (userAgent.Contains("Safari/") && !userAgent.Contains("Chrome/")) return "Safari";
        if (userAgent.Contains("OPR/")) return "Opera";
        return "Otro";
    }

    private static string? ExtraerOS(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("Windows")) return "Windows";
        if (userAgent.Contains("Mac OS X") || userAgent.Contains("Macintosh")) return "macOS";
        if (userAgent.Contains("Linux") && !userAgent.Contains("Android")) return "Linux";
        if (userAgent.Contains("Android")) return "Android";
        if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) return "iOS";
        return "Otro";
    }

}
