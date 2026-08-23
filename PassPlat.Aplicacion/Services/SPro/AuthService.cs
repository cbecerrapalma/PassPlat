using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CBP.Authentication.JwtBearer;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using CBP.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Options;
using PassPlat.Aplicacion.Services.Authentication;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.Repositories;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Enums;
using PassPlat.Aplicacion.Dtos.Contexto;

namespace PassPlat.Aplicacion.Services;

public interface IAuthService : ICustomService
{
    Task<Result<LoginResult>> LoginAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> LoginConTokenAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> CompletarLoginConMFAAsync(int idUsuario, int idTenant, int idApp, int idMFAPrincipal, string codigoMFA, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default);
    Task<Result> RevocarSesionPorJtiAsync(int idUsuario, string jti, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> PlatformLoginAsync(string nomUsuario, string password, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> SwitchTenantAsync(int idUsuario, int idTenant, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> SwitchToPlatformAsync(int idUsuario, string jti, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private static readonly Random _rng = Random.Shared;

    private readonly AuthRepository _authRepo;
    private readonly SesionRepository _sesionRepo;
    private readonly IIntentoAccesoService _intentoAccesoService;
    private readonly AuditoriaPwdRepository _auditoriaRepo;
    private readonly IJwtTokenService _jwtService;
    private readonly CBP.Security.Cryptography.Services.IPasswordService _pwdService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailQueue _emailQueue;
    private readonly IMFARepository _mfaRepo;
    private readonly IMfaCodeStore _mfaCodeStore;
    private readonly IOptions<MfaOptions> _mfaOptions;
    private readonly IIPService _ipService;
    private readonly IDispConfiableService _dispConfiableService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly IUsuarioTenantRepository _usuarioTenantRepo;
    private readonly SessionManager _sessionManager;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;

    public AuthService(
        AuthRepository authRepo,
        SesionRepository sesionRepo,
        IIntentoAccesoService intentoAccesoService,
        AuditoriaPwdRepository auditoriaRepo,
        IJwtTokenService jwtService,
        CBP.Security.Cryptography.Services.IPasswordService pwdService,
        JwtOptions jwtOptions,
        ILogger<AuthService> logger,
        IEmailQueue emailQueue,
        IMFARepository mfaRepo,
        IMfaCodeStore mfaCodeStore,
        IOptions<MfaOptions> mfaOptions,
        IIPService ipService,
        IDispConfiableService dispConfiableService,
        IHttpContextAccessor httpContextAccessor,
        IAuthenticationTokenService tokenService,
        IUsuarioTenantRepository usuarioTenantRepo,
        SessionManager sessionManager,
        CBP.Logging.Interfaces.ILoggerService olog)
    {
        _authRepo = authRepo;
        _sesionRepo = sesionRepo;
        _intentoAccesoService = intentoAccesoService;
        _auditoriaRepo = auditoriaRepo;
        _jwtService = jwtService;
        _pwdService = pwdService;
        _jwtOptions = jwtOptions;
        _logger = logger;
        _emailQueue = emailQueue;
        _mfaRepo = mfaRepo;
        _mfaCodeStore = mfaCodeStore;
        _mfaOptions = mfaOptions;
        _ipService = ipService;
        _dispConfiableService = dispConfiableService;
        _httpContextAccessor = httpContextAccessor;
        _tokenService = tokenService;
        _usuarioTenantRepo = usuarioTenantRepo;
        _sessionManager = sessionManager;
        _olog = olog;
    }

    private void LogAuthEvent(string eventName, string message, string? userId, int? tenantId)
    {
        _olog.LogInformation(new LogEvent
        {
            EventName = eventName,
            Scope = LoggingScopes.Authentication,
            Message = message,
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationAuth,
                [LoggingPropertyNames.Operation] = LoggingOperations.Authenticate,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = userId,
                [LoggingPropertyNames.TenantId] = tenantId,
            }
        });
    }

    public async Task<Result<LoginResult>> LoginAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var result = await _authRepo.LoginAsync(nomUsuario, email, idApp, hashPwdCalculado, idTenant, idDisp, idIP, idAgente, ct);
        if (result.IsFailure)
            return Result<LoginResult>.Failure(result.Error!);
        return result;
    }

    public async Task<Result<AuthResponseDto>> LoginConTokenAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var usuarioResult = await _authRepo.ObtenerUsuarioPorNomAsync(nomUsuario, email, ct);
        if (usuarioResult.IsFailure) return Result<AuthResponseDto>.Failure(usuarioResult.Error!);
        var usuario = usuarioResult.Value;

        string hashParaSP;
        if (usuario != null)
        {
            var hashResult = await _authRepo.ObtenerHashActualAsync(usuario.Id, ct);
            if (hashResult.IsFailure) return Result<AuthResponseDto>.Failure(hashResult.Error!);
            var storedHash = hashResult.Value;

            if (storedHash != null)
            {
                try
                {
                    var isValid = await _pwdService.VerifyAsync(storedHash, hashPwdCalculado, pepper: null, ct);
                    hashParaSP = isValid ? storedHash : hashPwdCalculado;
                }
                catch
                {
                    hashParaSP = hashPwdCalculado;
                }
            }
            else
            {
                hashParaSP = hashPwdCalculado;
            }
        }
        else
        {
            hashParaSP = hashPwdCalculado;
        }

        var loginResult = await _authRepo.LoginAsync(nomUsuario, email, idApp, hashParaSP, idTenant, idDisp, idIP, idAgente, ct);
        if (loginResult.IsFailure)
            return Result<AuthResponseDto>.Failure(loginResult.Error!);

        var login = loginResult.Value;

        if (login.Resultado != (int)EResultadoAcceso.Exitoso)
        {
            if (login.IdUsuario.HasValue)
            {
                await RegistrarAuditoriaAsync(login.IdUsuario.Value, ETipoAuditoria.LoginFallido, login.IdTenant, idApp, idDisp, idAgente, idIP, login.Mensaje ?? "Login rechazado", ct);

                if (login.IdBloqueo.HasValue)
                {
                    await NotificarBloqueoAsync(login.IdUsuario.Value, login.FecFinBloqueo, login.IdTenant, idApp, ct);
                }
                else if (login.IntentosRestantes.HasValue && login.IntentosRestantes.Value <= 2)
                {
                    await VerificarAlertaSeguridadAsync(login.IdUsuario.Value, nomUsuario ?? email ?? "?", idIP, login.IdTenant, idApp, ct);
                }
            }

            LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado",
                login.IdUsuario?.ToString() ?? nomUsuario ?? email ?? "?", login.IdTenant);
            return Result<AuthResponseDto>.Failure("LOGIN_FAILED", login.Mensaje ?? "Error de autenticación");
        }

        if (login.IdUsuario.HasValue)
            await RegistrarAuditoriaAsync(login.IdUsuario.Value, ETipoAuditoria.LoginExitoso, login.IdTenant, idApp, idDisp, idAgente, idIP, "Login exitoso", ct);

        if (login.IdMFAPrincipal.HasValue)
        {
            var tipoMfaResult = await ObtenerTipoMfaAsync(login.IdUsuario!.Value, ct);
            if (tipoMfaResult.IsFailure)
                return Result<AuthResponseDto>.Failure(tipoMfaResult.Error!);

            var envioResult = await EnviarCodigoMfaAsync(login.IdUsuario.Value, login.IdTenant!.Value, idApp, ct);
            if (envioResult.IsFailure)
            {
                _logger.LogError("No se pudo enviar el código MFA; se rechaza el login para evitar bypass de verificación. Error: {Error}", envioResult.Error?.Message);
                return Result<AuthResponseDto>.Failure(envioResult.Error!);
            }

            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                IdUsuario = login.IdUsuario.Value,
                IdTenant = login.IdTenant.Value,
                IdMFAPrincipal = login.IdMFAPrincipal,
                IdTipoMFA = tipoMfaResult.Value,
                ReqCambioPwd = login.ReqCambioPwd ?? false
            });
        }

        if (login.ReqCambioPwd == true)
        {
            var idUsuarioTenant = await ResolverIdUsuarioTenantAsync(login.IdUsuario!.Value, login.IdTenant!.Value, ct);
            var authContext = new AuthenticationContext(
                login.IdUsuario!.Value, login.IdTenant!.Value, idApp,
                (short?)idDisp, idIP, AuthenticationOrigin.Login,
                EsSistema: login.EsSistema,
                IdUsuarioTenant: idUsuarioTenant);
            var tokenResult = await _tokenService.LoginAsync(authContext, ct);
            if (tokenResult.IsFailure) return Result<AuthResponseDto>.Failure(tokenResult.Error!);

            var usuarioInfoResult = await _authRepo.ObtenerUsuarioBasicoAsync(login.IdUsuario.Value, ct);
            if (usuarioInfoResult.IsFailure) return Result<AuthResponseDto>.Failure(usuarioInfoResult.Error!);
            var usuarioInfo = usuarioInfoResult.Value;

            LogAuthEvent(LoggingEvents.LoginSucceeded, "Login exitoso (requiere cambio de contraseña)",
                login.IdUsuario.Value.ToString(), login.IdTenant);
            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                AccessToken = tokenResult.Value.AccessToken,
                RefreshToken = tokenResult.Value.RefreshToken,
                ExpiresAt = tokenResult.Value.ExpiresAt,
                IdUsuario = login.IdUsuario.Value,
                IdTenant = login.IdTenant.Value,
                NomUsuario = usuarioInfo?.NomUsuario ?? string.Empty,
                Email = usuarioInfo?.Email ?? string.Empty,
                Nombre = usuarioInfo?.Nombre ?? string.Empty,
                Apellido = usuarioInfo?.Apellido ?? string.Empty,
                ReqCambioPwd = true
            });
        }

        return await GenerarAuthResponseAsync(login, idApp, idDisp, idIP, ct);
    }

    public async Task<Result<AuthResponseDto>> CompletarLoginConMFAAsync(int idUsuario, int idTenant, int idApp, int idMFAPrincipal, string codigoMFA, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var mfaResult = await _mfaRepo.ObtenerMetodoPrincipalAsync(idUsuario, ct);
        if (mfaResult.IsFailure) return Result<AuthResponseDto>.Failure(mfaResult.Error!);
        var metodoMfa = mfaResult.Value;
        if (metodoMfa == null || metodoMfa.Id != idMFAPrincipal)
            return Result<AuthResponseDto>.Failure("MFA_INVALIDO", "Método MFA no encontrado");

        bool mfaValido;
        if (metodoMfa.IdTipoMFA == (int)ETipoMFA.Email)
        {
            mfaValido = await _mfaCodeStore.ValidateAndConsumeAsync(idUsuario, idTenant, codigoMFA, ct);
        }
        else
        {
            var validationResult = await _mfaRepo.ValidarMFAAsync(idUsuario, idTenant, metodoMfa.IdTipoMFA, codigoMFA, ct);
            if (validationResult.IsFailure)
                return Result<AuthResponseDto>.Failure(validationResult.Error!);
            mfaValido = validationResult.Value.Exito == 1;
        }

        if (!mfaValido)
        {
            _olog.LogInformation(new LogEvent
            {
                EventName = LoggingEvents.MfaFailed,
                Scope = LoggingScopes.Authentication,
                Message = "Código MFA inválido o expirado",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Authenticate,
                    [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                    [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                    [LoggingPropertyNames.TenantId] = idTenant,
                }
            });
            return Result<AuthResponseDto>.Failure("MFA_FALLIDO", "Código MFA inválido o expirado");
        }

        var idUsuarioTenant = await ResolverIdUsuarioTenantAsync(idUsuario, idTenant, ct);
        var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
        if (usuarioResult.IsFailure) return Result<AuthResponseDto>.Failure(usuarioResult.Error!);
        var usuario = usuarioResult.Value;
        if (usuario == null)
            return Result<AuthResponseDto>.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");

        var authContext = new AuthenticationContext(
            idUsuario, idTenant, idApp,
            (short?)idDisp, idIP, AuthenticationOrigin.Login,
            EsSistema: usuario.EsSistema,
            IdUsuarioTenant: idUsuarioTenant);
        var tokenResult = await _tokenService.LoginAsync(authContext, ct);
        if (tokenResult.IsFailure) return Result<AuthResponseDto>.Failure(tokenResult.Error!);

        await DetectarCambiosEnLoginAsync(idUsuario, idTenant, idIP, idDisp, idApp, ct);

        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.MfaSucceeded,
            Scope = LoggingScopes.Authentication,
            Message = "Login completado con MFA",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                [LoggingPropertyNames.Operation] = LoggingOperations.Authenticate,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = idTenant,
            }
        });
        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            AccessToken = tokenResult.Value.AccessToken,
            RefreshToken = tokenResult.Value.RefreshToken,
            ExpiresAt = tokenResult.Value.ExpiresAt,
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            NomUsuario = usuario?.NomUsuario ?? string.Empty,
            Email = usuario?.Email ?? string.Empty,
            Nombre = usuario?.Nombre ?? string.Empty,
            Apellido = usuario?.Apellido ?? string.Empty
        });
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hashRefresh = HashSHA256(refreshToken);
        var sesionResult = await _sesionRepo.ObtenerPorHashRefreshAsync(hashRefresh, ct);
        if (sesionResult.IsFailure) return Result<AuthResponseDto>.Failure(sesionResult.Error!);
        var sesion = sesionResult.Value;

        if (sesion == null)
        {
            _logger.LogWarning("Refresh token re-use detected (hash not found) — possible token theft");
            return Result<AuthResponseDto>.Failure("INVALID_REFRESH", "Refresh token inválido o expirado");
        }

        if (sesion.FecExpira <= DateTime.Now)
            return Result<AuthResponseDto>.Failure("REFRESH_EXPIRED", "Refresh token expirado");

        var idUsuarioTenant = await ResolverIdUsuarioTenantAsync(sesion.IdUsuario, sesion.IdTenant, ct);
        var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(sesion.IdUsuario, ct);
        if (usuarioResult.IsFailure) return Result<AuthResponseDto>.Failure(usuarioResult.Error!);
        var usuario = usuarioResult.Value;
        if (usuario == null)
            return Result<AuthResponseDto>.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");

        var authContext = new AuthenticationContext(
            sesion.IdUsuario, sesion.IdTenant, sesion.IdApp,
            IdDispositivo: null, IdIp: null, AuthenticationOrigin.Refresh,
            EsSistema: usuario.EsSistema,
            IdUsuarioTenant: idUsuarioTenant);
        var tokenResult = await _tokenService.RefreshAsync(authContext, sesion.HashRefresh, sesion.Id, ct);
        if (tokenResult.IsFailure) return Result<AuthResponseDto>.Failure(tokenResult.Error!);

        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.RefreshTokenIssued,
            Scope = LoggingScopes.Authentication,
            Message = "Refresh token emitido",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationAuth,
                [LoggingPropertyNames.Operation] = LoggingOperations.Refresh,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = sesion.IdUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = sesion.IdTenant,
            }
        });
        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            AccessToken = tokenResult.Value.AccessToken,
            RefreshToken = tokenResult.Value.RefreshToken,
            ExpiresAt = tokenResult.Value.ExpiresAt,
            IdUsuario = sesion.IdUsuario,
            IdTenant = sesion.IdTenant,
            NomUsuario = usuario?.NomUsuario ?? string.Empty,
            Email = usuario?.Email ?? string.Empty,
            Nombre = usuario?.Nombre ?? string.Empty,
            Apellido = usuario?.Apellido ?? string.Empty
        });
    }

    public async Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default)
    {
        var repoResult = await _sesionRepo.RevocarSesionAsync(idSesion, ct);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.Logout,
            Scope = LoggingScopes.Authentication,
            Message = "Sesión revocada (logout)",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationAuth,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
            }
        });
        return Result.Success();
    }

    public async Task<Result> RevocarSesionPorJtiAsync(int idUsuario, string jti, CancellationToken ct = default)
    {
        var resolved = await _sessionManager.ResolveAndRevokeSessionByJtiAsync(idUsuario, jti, ct);
        if (resolved.IsFailure) return Result.Failure(resolved.Error!);

        _olog.LogInformation(new LogEvent
        {
            EventName = LoggingEvents.Logout,
            Scope = LoggingScopes.Authentication,
            Message = "Sesión revocada (logout por jti)",
            Properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationAuth,
                [LoggingPropertyNames.Operation] = LoggingOperations.Execute,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = idUsuario.ToString(),
            }
        });
        return Result.Success();
    }

    private async Task<Result<int?>> ObtenerTipoMfaAsync(int idUsuario, CancellationToken ct)
    {
        try
        {
            var mfaResult = await _mfaRepo.ObtenerMetodoPrincipalAsync(idUsuario, ct);
            if (mfaResult.IsFailure)
                return Result<int?>.Failure(mfaResult.Error!);
            return Result<int?>.Success(mfaResult.Value?.IdTipoMFA, allowNull: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener método MFA principal para usuario {IdUsuario}", idUsuario);
            return Result<int?>.Failure("MFA_READ_ERROR", "Error al obtener el método MFA principal");
        }
    }

    private async Task<Result> EnviarCodigoMfaAsync(int idUsuario, int idTenant, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var mfaResult = await _mfaRepo.ObtenerMetodoPrincipalAsync(idUsuario, ct);
            if (mfaResult.IsFailure)
                return Result.Failure(mfaResult.Error!);
            var metodoMfa = mfaResult.Value;
            if (metodoMfa == null || metodoMfa.IdTipoMFA != (int)ETipoMFA.Email)
                return Result.Success();

            var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (usuarioResult.IsFailure)
                return Result.Failure(usuarioResult.Error!);
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return Result.Failure("MFA_SIN_EMAIL", "El usuario no tiene email configurado para recibir el código MFA");

            var cfg = _mfaOptions.Value;
            var min = (int)Math.Pow(10, cfg.LongitudCodigoMFA - 1);
            var max = (int)Math.Pow(10, cfg.LongitudCodigoMFA) - 1;
            var code = _rng.Next(min, max).ToString($"D{cfg.LongitudCodigoMFA}");
            await _mfaCodeStore.StoreAsync(idUsuario, idTenant, code, TimeSpan.FromMinutes(cfg.TiempoValidezCodigoMFA), ct);
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.MfaCode,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["MfaCode"] = code,
                    ["ExpiraMinutos"] = cfg.TiempoValidezCodigoMFA
                },
                usuario.IdTenant,
                usuario.Id,
                idApp,
                null), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar código MFA a usuario {IdUsuario}", idUsuario);
            return Result.Failure("MFA_SEND_ERROR", "Error al enviar el código MFA por email");
        }
    }

    private async Task<Result<AuthResponseDto>> GenerarAuthResponseAsync(LoginResult login, int idApp, int? idDisp, int? idIP, CancellationToken ct)
    {
        var idUsuarioTenant = await ResolverIdUsuarioTenantAsync(login.IdUsuario!.Value, login.IdTenant!.Value, ct);
        var authContext = new AuthenticationContext(
            login.IdUsuario!.Value, login.IdTenant!.Value, idApp,
            (short?)idDisp, idIP, AuthenticationOrigin.Login,
            EsSistema: login.EsSistema,
            IdUsuarioTenant: idUsuarioTenant);
        var tokenResult = await _tokenService.LoginAsync(authContext, ct);
        if (tokenResult.IsFailure) return Result<AuthResponseDto>.Failure(tokenResult.Error!);

        var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(login.IdUsuario.Value, ct);
        if (usuarioResult.IsFailure) return Result<AuthResponseDto>.Failure(usuarioResult.Error!);
        var usuario = usuarioResult.Value;

        await DetectarCambiosEnLoginAsync(login.IdUsuario.Value, login.IdTenant.Value, idIP, idDisp, idApp, ct);

        LogAuthEvent(LoggingEvents.LoginSucceeded, "Login exitoso",
            login.IdUsuario.Value.ToString(), login.IdTenant);
        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            AccessToken = tokenResult.Value.AccessToken,
            RefreshToken = tokenResult.Value.RefreshToken,
            ExpiresAt = tokenResult.Value.ExpiresAt,
            IdUsuario = login.IdUsuario.Value,
            IdTenant = login.IdTenant.Value,
            NomUsuario = usuario?.NomUsuario ?? string.Empty,
            Email = usuario?.Email ?? string.Empty,
            Nombre = usuario?.Nombre ?? string.Empty,
            Apellido = usuario?.Apellido ?? string.Empty,
            ReqCambioPwd = login.ReqCambioPwd ?? false
        });
    }

    private async Task DetectarCambiosEnLoginAsync(int idUsuario, int idTenant, int? idIP, int? idDisp, int idApp, CancellationToken ct)
    {
        try
        {
            string? direccionIP = null;

            if (idIP.HasValue)
            {
                var ipDtoResult = await _ipService.GetByIdAsync(idIP.Value, ct);
                if (ipDtoResult.IsSuccess && ipDtoResult.Value != null)
                    direccionIP = ipDtoResult.Value.Direccion;
            }

            if (!string.IsNullOrEmpty(direccionIP))
            {
                await _ipService.DetectarNuevaIPAsync(idUsuario, idTenant, direccionIP, ct: ct);
                await _ipService.VerificarCambioIPAsync(idUsuario, idTenant, direccionIP, ct);
            }

            if (idDisp.HasValue)
            {
                await _dispConfiableService.DetectarNuevoDispositivoAsync(idUsuario, idTenant, idDisp.Value, null, null, direccionIP, null, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en detección de IP/dispositivo durante login para usuario {IdUsuario}", idUsuario);
        }
    }

    private async Task RegistrarAuditoriaAsync(int idUsuario, ETipoAuditoria tipoAccion, int? idTenant, int? idApp, int? idDisp, int? idAgente, int? idIP, string? detalles, CancellationToken ct)
    {
        _auditoriaRepo.RegistrarAuditoria(idUsuario, (int)tipoAccion, idTenant, idApp, idUsrEjecutor: idUsuario, idDisp, idAgente, idIP, detalles: detalles);
    }

    private async Task NotificarBloqueoAsync(int idUsuario, DateTime? fecFin, int? idTenant, int? idApp, CancellationToken ct)
    {
        try
        {
            var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return;
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return;

            var minutes = fecFin.HasValue
                ? Math.Max(1, (int)(fecFin.Value - DateTime.Now).TotalMinutes)
                : 30;

            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.AccountLocked,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["Minutes"] = minutes },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de bloqueo para usuario {IdUsuario}", idUsuario);
        }
    }

    private async Task VerificarAlertaSeguridadAsync(int idUsuario, string nomUsuario, int? idIP, int? idTenant, int? idApp, CancellationToken ct)
    {
        try
        {
            var countResult = await _intentoAccesoService.ContarIntentosFallidosRecientesAsync(idUsuario, 15, ct);
            if (countResult.IsFailure) return;
            var count = countResult.Value;
            if (count < 3)
                return;

            var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return;
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return;

            var alertMsg = $"Se detectaron {count} intentos de inicio de sesión fallidos en los últimos 15 minutos.";
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.SecurityAlert,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["AlertMessage"] = alertMsg,
                    ["Ip"] = idIP?.ToString() ?? "Desconocida"
                },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar/encolar alerta de seguridad para usuario {IdUsuario}", idUsuario);
        }
    }

    public async Task<Result<AuthResponseDto>> PlatformLoginAsync(string nomUsuario, string password, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _authRepo.ObtenerUsuarioPorNomAsync(nomUsuario, null, ct);
            if (usuarioResult.IsFailure || usuarioResult.Value == null)
            {
                LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado (plataforma): usuario no encontrado", null, null);
                return Result<AuthResponseDto>.Failure("LOGIN_FAILED", "Credenciales inválidas");
            }

            var usuario = usuarioResult.Value;

            if (usuario.Eliminado || usuario.IdEstado == (int)EEstadoUsuario.Eliminado)
            {
                LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado (plataforma): cuenta eliminada", usuario.Id.ToString(), null);
                return Result<AuthResponseDto>.Failure("CUENTA_ELIMINADA", "La cuenta ha sido eliminada");
            }

            if (usuario.IdEstado != (int)EEstadoUsuario.Activo)
            {
                LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado (plataforma): cuenta inactiva", usuario.Id.ToString(), null);
                return Result<AuthResponseDto>.Failure("CUENTA_INACTIVA", "La cuenta no está activa");
            }

            var hashResult = await _authRepo.ObtenerHashActualAsync(usuario.Id, ct);
            if (hashResult.IsFailure || hashResult.Value == null)
            {
                LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado (plataforma): hash no disponible", usuario.Id.ToString(), null);
                return Result<AuthResponseDto>.Failure("LOGIN_FAILED", "Credenciales inválidas");
            }

            var isValid = await _pwdService.VerifyAsync(hashResult.Value, password, pepper: null, ct);
            if (!isValid)
            {
                LogAuthEvent(LoggingEvents.LoginFailed, "Login rechazado (plataforma): contraseña inválida", usuario.Id.ToString(), null);
                return Result<AuthResponseDto>.Failure("LOGIN_FAILED", "Credenciales inválidas");
            }

            var authContext = new AuthenticationContext(
                usuario.Id, null, idApp,
                (short?)idDisp, idIP, AuthenticationOrigin.Login,
                EsSistema: usuario.EsSistema);

            var tokenResult = await _tokenService.LoginAsync(authContext, ct);
            if (tokenResult.IsFailure)
                return Result<AuthResponseDto>.Failure(tokenResult.Error!);

            LogAuthEvent(LoggingEvents.LoginSucceeded, "Login exitoso (plataforma)", usuario.Id.ToString(), null);

            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                AccessToken = tokenResult.Value.AccessToken,
                RefreshToken = tokenResult.Value.RefreshToken,
                ExpiresAt = tokenResult.Value.ExpiresAt,
                IdUsuario = usuario.Id,
                IdTenant = 0,
                NomUsuario = usuario.NomUsuario ?? string.Empty,
                Email = usuario.Email ?? string.Empty,
                Nombre = usuario.Nombre ?? string.Empty,
                Apellido = usuario.Apellido ?? string.Empty,
                ReqCambioPwd = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en platform login para usuario {NomUsuario}", nomUsuario);
            return Result<AuthResponseDto>.Failure("LOGIN_ERROR", "Error interno al procesar el inicio de sesión");
        }
    }

    public async Task<Result<AuthResponseDto>> SwitchTenantAsync(int idUsuario, int idTenant, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        try
        {
            var membresiaResult = await _usuarioTenantRepo.ObtenerActivoPorTenantAsync(idUsuario, idTenant, ct);
            if (membresiaResult.IsFailure || membresiaResult.Value == null)
                return Result<AuthResponseDto>.Failure("SIN_ACCESO_TENANT", "No tienes acceso a este tenant");

            var membresia = membresiaResult.Value;

            if (!membresia.Activo || membresia.IdEstado != (int)EEstadoUsuario.Activo)
                return Result<AuthResponseDto>.Failure("SIN_ACCESO_TENANT", "No tienes acceso activo a este tenant");

            var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (usuarioResult.IsFailure)
                return Result<AuthResponseDto>.Failure(usuarioResult.Error!);
            var usuario = usuarioResult.Value;
            if (usuario == null)
                return Result<AuthResponseDto>.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");

            var authContext = new AuthenticationContext(
                idUsuario, idTenant, idApp,
                (short?)idDisp, idIP, AuthenticationOrigin.SwitchTenant,
                EsSistema: usuario.EsSistema,
                IdUsuarioTenant: membresia.Id);

            var tokenResult = await _tokenService.LoginAsync(authContext, ct);
            if (tokenResult.IsFailure)
                return Result<AuthResponseDto>.Failure(tokenResult.Error!);

            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                AccessToken = tokenResult.Value.AccessToken,
                RefreshToken = tokenResult.Value.RefreshToken,
                ExpiresAt = tokenResult.Value.ExpiresAt,
                IdUsuario = idUsuario,
                IdTenant = idTenant,
                NomUsuario = usuario?.NomUsuario ?? string.Empty,
                Email = usuario?.Email ?? string.Empty,
                Nombre = usuario?.Nombre ?? string.Empty,
                Apellido = usuario?.Apellido ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en switch tenant para usuario {IdUsuario} tenant {IdTenant}", idUsuario, idTenant);
            return Result<AuthResponseDto>.Failure("SWITCH_ERROR", "Error interno al cambiar de tenant");
        }
    }

    public async Task<Result<AuthResponseDto>> SwitchToPlatformAsync(int idUsuario, string jti, int idApp, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
            if (usuarioResult.IsFailure || usuarioResult.Value == null)
                return Result<AuthResponseDto>.Failure("USUARIO_NO_ENCONTRADO", "Usuario no encontrado");

            var usuario = usuarioResult.Value;

            if (usuario.Eliminado || usuario.IdEstado == (int)EEstadoUsuario.Eliminado)
                return Result<AuthResponseDto>.Failure("CUENTA_ELIMINADA", "La cuenta ha sido eliminada");

            if (usuario.IdEstado != (int)EEstadoUsuario.Activo)
                return Result<AuthResponseDto>.Failure("CUENTA_INACTIVA", "La cuenta no está activa");

            var accesoPlatformResult = await _authRepo.ExisteAccesoPlatformActivoAsync(idUsuario, idApp, ct);
            if (accesoPlatformResult.IsFailure)
                return Result<AuthResponseDto>.Failure(accesoPlatformResult.Error!);

            if (!accesoPlatformResult.Value)
                return Result<AuthResponseDto>.Failure("SIN_ACCESO_PLATFORM", "No tienes autorización para el scope de plataforma");

            var revokeResult = await _sessionManager.ResolveAndRevokeSessionByJtiAsync(idUsuario, jti, ct);
            if (revokeResult.IsFailure)
                return Result<AuthResponseDto>.Failure(revokeResult.Error!);

            if (revokeResult.Value == null)
                return Result<AuthResponseDto>.Failure("SESION_REVOCADA", "El JWT ya no tiene una sesión activa");

            var authContext = new AuthenticationContext(
                idUsuario, null, idApp,
                (short?)idDisp, idIP, AuthenticationOrigin.SwitchToPlatform,
                EsSistema: usuario.EsSistema);

            var tokenResult = await _tokenService.LoginAsync(authContext, ct);
            if (tokenResult.IsFailure)
                return Result<AuthResponseDto>.Failure(tokenResult.Error!);

            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                AccessToken = tokenResult.Value.AccessToken,
                RefreshToken = tokenResult.Value.RefreshToken,
                ExpiresAt = tokenResult.Value.ExpiresAt,
                IdUsuario = idUsuario,
                IdTenant = 0,
                NomUsuario = usuario.NomUsuario ?? string.Empty,
                Email = usuario.Email ?? string.Empty,
                Nombre = usuario.Nombre ?? string.Empty,
                Apellido = usuario.Apellido ?? string.Empty,
                ReqCambioPwd = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en switch-to-platform para usuario {IdUsuario}", idUsuario);
            return Result<AuthResponseDto>.Failure("SWITCH_PLATFORM_ERROR", "Error interno al cambiar a platform scope");
        }
    }

    private static string HashSHA256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private async Task<int?> ResolverIdUsuarioTenantAsync(int idUsuario, int idTenant, CancellationToken ct)
    {
        var result = await _usuarioTenantRepo.ResolverIdUsuarioTenantAsync(idUsuario, idTenant, ct);
        return result.IsSuccess ? result.Value : null;
    }
}
