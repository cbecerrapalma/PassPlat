using System.Text.Json;
using CBP.Data.Abstractions;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using CBP.Security.Cryptography.Models;
using CBP.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Enums;
using ValidationResult = CBP.Security.Cryptography.Models.ValidationResult;

namespace PassPlat.Aplicacion.Services;

public interface IPasswordService : ICustomService
{
    Task<Result<CambiarPwdResult>> CambiarPasswordAsync(int idUsuario, int idTenant, string hashPwdNuevo, byte pepperVersion, int idTipoCambio, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<bool>> ValidarPasswordRepetidaAsync(int idUsuario, string hashPwd, int historialCant, CancellationToken ct = default);
    Task<Result> DesactivarPasswordActualAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<CambiarPwdResult>> AdminCambiarPasswordAsync(int idUsuario, int idTenant, string nuevaPassword, int? idUsrEjecutor = null, string? ipAddress = null, string? userAgent = null, string? correlationId = null, CancellationToken ct = default);
    Task<Result<string>> HashPasswordAsync(string password, CancellationToken ct = default);
    Task<Result<string>> HashPasswordAsync(string password, CBP.Security.Cryptography.Models.PoliticaPwd policy, CancellationToken ct = default);
    Task<Result<PasswordStrengthInfoDto>> ValidarPasswordFortalezaAsync(string password, PoliticaPwdDto policy, string? nomUsuario = null, string? email = null, CancellationToken ct = default);
    Task<Result> ValidarPasswordActualAsync(int idUsuario, string passwordActual, CancellationToken ct = default);
}

public class PasswordService : IPasswordService
{
    private readonly PasswordRepository _passwordRepo;
    private readonly IHistorialPwdRepository _historialRepo;
    private readonly AuthRepository _authRepo;
    private readonly CBP.Security.Cryptography.Services.IPasswordService _pwdService;
    private readonly IPassPlatPasswordSecurity _passwordSecurity;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly IAuditoriaPwdService _auditoriaService;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ILogger<PasswordService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PasswordService(
        PasswordRepository passwordRepo,
        IHistorialPwdRepository historialRepo,
        AuthRepository authRepo,
        CBP.Security.Cryptography.Services.IPasswordService pwdService,
        IPassPlatPasswordSecurity passwordSecurity,
        IUsuarioRepository usuarioRepo,
        IEmailQueue emailQueue,
        IAuditoriaPwdService auditoriaService,
        IUnitOfWorkAsync uow,
        ILogger<PasswordService> logger,
        CBP.Logging.Interfaces.ILoggerService olog,
        IHttpContextAccessor httpContextAccessor)
    {
        _passwordRepo = passwordRepo;
        _historialRepo = historialRepo;
        _authRepo = authRepo;
        _pwdService = pwdService;
        _passwordSecurity = passwordSecurity;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _auditoriaService = auditoriaService;
        _uow = uow;
        _logger = logger;
        _olog = olog;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<CambiarPwdResult>> CambiarPasswordAsync(int idUsuario, int idTenant, string hashPwdNuevo, byte pepperVersion, int idTipoCambio, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var result = await _passwordRepo.CambiarPasswordAsync(idUsuario, idTenant, hashPwdNuevo, pepperVersion, idTipoCambio, idDisp, idIP, idAgente, ct);
        if (result.IsSuccess)
        {
            var evento = idTipoCambio == (int)ETipoCambioPwd.Reset
                ? LoggingEvents.PasswordReset
                : LoggingEvents.PasswordChanged;
            var mensaje = idTipoCambio == (int)ETipoCambioPwd.Reset
                ? "Contraseña restablecida"
                : "Contraseña cambiada";
            var properties = new Dictionary<string, object?>
            {
                [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                [LoggingPropertyNames.Operation] = idTipoCambio == (int)ETipoCambioPwd.Reset
                    ? LoggingOperations.Execute
                    : LoggingOperations.Update,
                [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                [LoggingPropertyNames.UserId] = idUsuario.ToString(),
                [LoggingPropertyNames.TenantId] = idTenant,
            };
            var logEvent = new LogEvent
            {
                EventName = evento,
                Scope = LoggingScopes.PasswordPolicy,
                Message = mensaje,
                Properties = properties
            };

            if (idTipoCambio == (int)ETipoCambioPwd.Reset)
                _olog.LogWarning(logEvent);
            else
                _olog.LogInformation(logEvent);

            await NotificarCambioPasswordAsync(idUsuario, idTipoCambio, idTenant, null, ct);
        }
        return result;
    }

    public async Task<Result<bool>> ValidarPasswordRepetidaAsync(int idUsuario, string hashPwd, int historialCant, CancellationToken ct = default)
    {
        var repetidaResult = await _historialRepo.PasswordRepetidaAsync(idUsuario, hashPwd, historialCant, ct);
        if (repetidaResult.IsFailure) return Result<bool>.Failure(repetidaResult.Error!);
        return Result<bool>.Success(repetidaResult.Value);
    }

    public async Task<Result> DesactivarPasswordActualAsync(int idUsuario, CancellationToken ct = default)
    {
        var repoResult = _historialRepo.DesactivarPasswordActual(idUsuario);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);
        return Result.Success();
    }

    public async Task<Result<string>> HashPasswordAsync(string password, CancellationToken ct = default)
    {
        return await HashPasswordAsync(password, PoliticaPermisiva(), ct);
    }

    public async Task<Result<string>> HashPasswordAsync(string password, CBP.Security.Cryptography.Models.PoliticaPwd policy, CancellationToken ct = default)
    {
        var creationResult = await _pwdService.CreatePasswordAsync(password, policy, pepper: null, cancellationToken: ct);
        if (!creationResult.Success || creationResult.HashInfo == null)
        {
            var errors = creationResult.Errors is { Count: > 0 }
                ? string.Join("; ", creationResult.Errors)
                : "Error al generar el hash de la contraseña";
            return Result<string>.Failure("PWD_POLICY_FAILED", errors);
        }
        return Result<string>.Success(creationResult.HashInfo.Hash);
    }

    private static PoliticaPwd PoliticaPermisiva() => new()
    {
        Codigo = "ADMIN_OVERRIDE",
        Nombre = "Admin Password Override",
        LongMin = 1,
        LongMax = 128,
        ReqMayuscula = false,
        ReqMinuscula = false,
        ReqNumero = false,
        ReqEspecial = false,
        ProhSecuenciales = false,
        ProhRepetitivos = false,
        ProhPatrones = false,
        ProhPwdComun = false,
        ProhInfoUsuario = false,
        VerificarBrechas = false,
        PermitirEspacios = true,
        DiasVigencia = 90,
        PwdRecordadas = 0,
        MaxIntentos = 5,
        DurBloqueoMin = 30,
        Activa = true,
        Version = 1
    };

    public async Task<Result<CambiarPwdResult>> AdminCambiarPasswordAsync(int idUsuario, int idTenant, string nuevaPassword, int? idUsrEjecutor = null, string? ipAddress = null, string? userAgent = null, string? correlationId = null, CancellationToken ct = default)
    {
        var policy = PoliticaPermisiva();
        var creationResult = await _pwdService.CreatePasswordAsync(nuevaPassword, policy, pepper: null, cancellationToken: ct);
        if (!creationResult.Success || creationResult.HashInfo == null)
            return Result<CambiarPwdResult>.Failure("PWD_CREATION_FAILED", "Error al generar el hash de la nueva contraseña");

        var hash = creationResult.HashInfo.Hash;
        var result = await CambiarPasswordAsync(idUsuario, idTenant, hash, pepperVersion: 1, idTipoCambio: (int)ETipoCambioPwd.Forzado, idDisp: null, idIP: null, idAgente: null, ct);

        if (result.IsSuccess)
        {
            try
            {
                var metadata = correlationId != null || ipAddress != null || userAgent != null
                    ? JsonSerializer.Serialize(new { CorrelationId = correlationId, IpAddress = ipAddress, UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent })
                    : null;

                var detalles = $"Cambio de contraseña forzado por administrador. IP: {ipAddress ?? "N/A"}";
                if (!string.IsNullOrEmpty(userAgent))
                    detalles += $", User-Agent: {(userAgent.Length > 100 ? userAgent[..100] + "..." : userAgent)}";

                var auditoriaDto = new RegistrarAuditoriaPwdDto
                {
                    IdUsuario = idUsuario,
                    IdTipoAccion = (int)ETipoAuditoria.CambioPassword,
                    IdTenant = idTenant,
                    IdUsrEjecutor = idUsrEjecutor,
                    Detalles = detalles,
                    NivelRiesgo = 3,
                    Metadata = metadata
                };

                await _auditoriaService.RegistrarAuditoriaAsync(auditoriaDto, ct);
                await _uow.SaveChangesAsync(ct);

                _logger.LogInformation("AdminCambiarPassword: UsrEjecutor={IdUsrEjecutor}, IdUsuario={IdUsuario}, Tenant={IdTenant}, IP={IpAddress}, CorrelationId={CorrelationId}",
                    idUsrEjecutor, idUsuario, idTenant, ipAddress, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar auditoría de cambio de password admin para usuario {IdUsuario}", idUsuario);
            }
        }

        return result;
    }

    public async Task<Result> ValidarPasswordActualAsync(int idUsuario, string passwordActual, CancellationToken ct = default)
    {
        var hashResult = await _authRepo.ObtenerHashActualAsync(idUsuario, ct);
        if (hashResult.IsFailure) return Result.Failure(hashResult.Error!);
        var storedHash = hashResult.Value;
        if (storedHash == null)
            return Result.Success();

        var isValid = await _pwdService.VerifyAsync(storedHash, passwordActual, pepper: null, ct);
        return isValid
            ? Result.Success()
            : Result.Failure("INVALID_CURRENT_PASSWORD", "La contraseña actual no es correcta");
    }

    public async Task<Result<PasswordStrengthInfoDto>> ValidarPasswordFortalezaAsync(string password, PoliticaPwdDto policy, string? nomUsuario = null, string? email = null, CancellationToken ct = default)
    {
        var cbpPolicy = new CBP.Security.Cryptography.Models.PoliticaPwd
        {
            LongMin = policy.LongMin,
            LongMax = policy.LongMax,
            ReqMayuscula = policy.ReqMayuscula,
            ReqMinuscula = policy.ReqMinuscula,
            ReqNumero = policy.ReqNumero,
            ReqEspecial = policy.ReqEspecial,
            CaracteresEspeciales = policy.CaracteresEspeciales,
            ProhSecuenciales = policy.ProhSecuenciales,
            ProhRepetitivos = policy.ProhRepetitivos,
            ProhPatrones = policy.ProhPatrones,
            ProhPwdComun = policy.ProhPwdComun,
            ProhInfoUsuario = policy.ProhInfoUsuario,
            VerificarBrechas = policy.VerificarBrechas,
            PermitirEspacios = policy.PermitirEspacios
        };

        var context = new CBP.Security.Cryptography.Models.ValidationContext
        {
            UserName = nomUsuario,
            Email = email
        };

        var validation = await _passwordSecurity.ValidatePasswordAsync(password, cbpPolicy, context, ct);
        var analysis = await _passwordSecurity.AnalyzePasswordAsync(password, context, ct);

        var result = new PasswordStrengthInfoDto
        {
            Score = (int)analysis.StrengthScore,
            Level = analysis.StrengthLevel.ToString(),
            Length = analysis.Length,
            HasUppercase = analysis.HasUppercase,
            HasLowercase = analysis.HasLowercase,
            HasNumbers = analysis.HasNumbers,
            HasSpecialCharacters = analysis.HasSpecialCharacters,
            IsCommon = analysis.IsCommon,
            HasSequentialChars = analysis.HasSequentialChars,
            HasRepeatingChars = analysis.HasRepeatingChars,
            HasKeyboardPatterns = analysis.HasKeyboardPatterns,
            ContainsUserInfo = analysis.ContainsUserInfo,
            Warnings = analysis.Warnings.ToList(),
            Recommendations = analysis.Recommendations.ToList(),
            IsValid = validation.IsValid,
            Errors = validation.ErrorMessages
        };

        if (!validation.IsValid)
        {
            _olog.LogWarning(new LogEvent
            {
                EventName = LoggingEvents.PasswordPolicyViolation,
                Scope = LoggingScopes.PasswordPolicy,
                Message = "Contraseña rechazada por política",
                Properties = new Dictionary<string, object?>
                {
                    [LoggingPropertyNames.Category] = LoggingCategories.ApplicationSecurity,
                    [LoggingPropertyNames.Operation] = LoggingOperations.Validate,
                    [LoggingPropertyNames.CorrelationId] = _httpContextAccessor.HttpContext?.Items[LoggingPropertyNames.HttpCorrelationIdKey] as string,
                }
            });
        }

        return Result<PasswordStrengthInfoDto>.Success(result);
    }

    private async Task NotificarCambioPasswordAsync(int idUsuario, int idTipoCambio, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        idApp ??= 1;
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return;
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return;

            var tipoCambio = (ETipoCambioPwd)idTipoCambio;
            var appId = idApp ?? 1;

            if (tipoCambio == ETipoCambioPwd.PrimerUso)
            {
                await _emailQueue.EnqueueAsync(new EmailJob(
                    EmailJobKind.FirstLogin,
                    usuario.Email,
                    usuario.NomUsuario,
                    new Dictionary<string, object?> { ["AppName"] = "PassPlat" },
                    idTenant,
                    usuario.Id,
                    appId,
                    null), ct);
            }

            var tipoTexto = tipoCambio.ToString();
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.PasswordChanged,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["TipoCambio"] = tipoTexto },
                idTenant,
                usuario.Id,
                appId,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de cambio de password para usuario {IdUsuario}", idUsuario);
        }
    }
}
