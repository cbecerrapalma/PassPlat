using System.Text.Json;
using CBP.Data.Abstractions;
using CBP.Emails.Configuration;
using CBP.Emails.Core.Models;
using CBP.Emails.Services;
using CBP.Logging;
using CBP.Logging.Models;
using CBP.Results;
using Microsoft.Extensions.Options;
using PassPlat.Aplicacion.Options;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using Microsoft.Extensions.Logging;

namespace PassPlat.Aplicacion.Services.Email;

public class PassPlatEmailService : IPassPlatEmailService
{
    private readonly IEmailAccountResolverService _accountResolver;
    private readonly IEmailLogRepository _emailLogRepo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailTemplateRepository _templateRepo;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IEmailTemplateStoreService _templateStore;
    private readonly ILogger<PassPlatEmailService> _logger;
    private readonly CBP.Logging.Interfaces.ILoggerService _olog;
    private readonly IOptions<MfaOptions> _mfaOptions;

    public PassPlatEmailService(
        IEmailAccountResolverService accountResolver,
        IEmailLogRepository emailLogRepo,
        IUsuarioRepository usuarioRepo,
        IEmailTemplateRepository templateRepo,
        IUnitOfWorkAsync uow,
        IEmailTemplateStoreService templateStore,
        ILogger<PassPlatEmailService> logger,
        CBP.Logging.Interfaces.ILoggerService olog,
        IOptions<MfaOptions> mfaOptions)
        {
            _accountResolver = accountResolver;
            _emailLogRepo = emailLogRepo;
            _usuarioRepo = usuarioRepo;
            _templateRepo = templateRepo;
            _uow = uow;
            _templateStore = templateStore;
            _logger = logger;
            _olog = olog;
            _mfaOptions = mfaOptions;
        }

    public async Task InvalidateCacheAsync(CancellationToken ct = default)
    {
        await _templateStore.InvalidateAllCacheAsync(ct);
    }

    public async Task<Result<EmailResult>> SendPasswordResetAsync(string toEmail, string userName, string resetLink, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["ResetLink"] = resetLink,
            ["ExpiraMinutos"] = 60
        };
        return await SendFromTemplateAsync("password-reset", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendMfaCodeAsync(string toEmail, string userName, string code, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["MfaCode"] = code,
            ["ExpiraMinutos"] = _mfaOptions.Value.TiempoValidezCodigoMFA
        };
        return await SendFromTemplateAsync("mfa-code", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendWelcomeAsync(string toEmail, string userName, string appName, string loginUrl, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["AppName"] = appName,
            ["LoginUrl"] = loginUrl
        };
        return await SendFromTemplateAsync("welcome", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendSecurityAlertAsync(string toEmail, string userName, string alertMessage, string ip, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["AlertMessage"] = alertMessage,
            ["IP"] = ip,
            ["FechaHora"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return await SendFromTemplateAsync("security-alert", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendAccountLockedAsync(string toEmail, string userName, int minutes, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["Minutes"] = minutes
        };
        return await SendFromTemplateAsync("account-locked", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendPasswordChangedAsync(string toEmail, string userName, string tipoCambio, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["TipoCambio"] = tipoCambio,
            ["FechaHora"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return await SendFromTemplateAsync("password-changed", toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendNotificationAsync(string templateCode, string toEmail, string userName, IReadOnlyDictionary<string, object?>? extraVariables = null, CancellationToken ct = default)
    {
        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["FechaHora"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        if (extraVariables != null)
        {
            foreach (var kv in extraVariables)
                vars[kv.Key] = kv.Value;
        }
        return await SendFromTemplateAsync(templateCode, toEmail, vars, ct: ct);
    }

    public async Task<Result<EmailResult>> SendFromTemplateAsync(string templateCode, string toEmail, IReadOnlyDictionary<string, object?> variables, string? accountGroup = null, CancellationToken ct = default)
    {
        var subjectResult = await _templateStore.RenderSubjectAsync(templateCode, variables, ct: ct);
        if (subjectResult.IsFailure)
            return Result<EmailResult>.Failure(subjectResult.Error!);

        var bodyResult = await _templateStore.RenderBodyAsync(templateCode, variables, ct: ct);
        if (bodyResult.IsFailure)
            return Result<EmailResult>.Failure(bodyResult.Error!);

        var templateResult = await _templateRepo.ObtenerPorNombreCulturaAsync(templateCode, "es", null, ct);
        var idTemplate = templateResult.IsSuccess ? templateResult.Value?.Id : null;

        return await SendEmailAsync(toEmail, subjectResult.Value, bodyResult.Value, true, null, null, null, ct, null, null, null, idTemplate);
    }

    public async Task<Result<EmailResult>> SendRawAsync(string toEmail, string subject, string body, bool isHtml = true, string? accountGroup = null, CancellationToken ct = default)
    {
        return await SendEmailAsync(toEmail, subject, body, isHtml, null, null, null, ct, null, null, null, null);
    }

    public async Task<Result<EmailResult>> SendFromJobAsync(EmailJob job, CancellationToken ct = default)
    {
        return await SendFromTemplateWithJobAsync(job, ct);
    }

    private async Task<Result<EmailResult>> SendFromTemplateWithJobAsync(EmailJob job, CancellationToken ct)
    {
        // Allow override via Extra["TemplateCode"] (e.g. password-expiration-15/7/3/1)
        var templateCode = job.Extra.TryGetValue("TemplateCode", out var tc) && tc is string tcs && !string.IsNullOrWhiteSpace(tcs)
            ? tcs
            : job.Kind switch
        {
            EmailJobKind.PasswordReset => "password-reset",
            EmailJobKind.MfaCode => "mfa-code",
            EmailJobKind.Welcome => "welcome",
            EmailJobKind.SecurityAlert => "security-alert",
            EmailJobKind.AccountLocked => "account-locked",
            EmailJobKind.PasswordChanged => "password-changed",
            EmailJobKind.UserActivated => "user-activated",
            EmailJobKind.UserDeactivated => "user-deactivated",
            EmailJobKind.UserUnblocked => "user-unblocked",
            EmailJobKind.PasswordExpired => "password-expired",
            EmailJobKind.FirstLogin => "first-login",
            EmailJobKind.MfaEnabled => "mfa-enabled",
            EmailJobKind.MfaDisabled => "mfa-disabled",
            EmailJobKind.NewDevice => "new-device",
            EmailJobKind.DeviceRevoked => "device-revoked",
            EmailJobKind.NewIp => "new-ip",
            EmailJobKind.RoleAssigned => "role-assigned",
            EmailJobKind.RoleRemoved => "role-removed",
            EmailJobKind.TenantCreated => "tenant-created",
            EmailJobKind.TenantSuspended => "tenant-suspended",
            EmailJobKind.TenantReactivated => "tenant-reactivated",
            EmailJobKind.AppRegistered => "app-registered",
            EmailJobKind.ExternalLogin => "external-login",
            EmailJobKind.ExternalIdentityLinked => "external-identity-linked",
            EmailJobKind.ExternalIdentityUnlinked => "external-identity-unlinked",
            EmailJobKind.ProviderAdded => "provider-added",
            EmailJobKind.ProviderRemoved => "provider-removed",
            EmailJobKind.AuthError => "auth-error",
            EmailJobKind.ProviderPrincipalChanged => "provider-principal-changed",
            EmailJobKind.PasswordLocalAdded => "password-local-added",
            EmailJobKind.PasswordLocalRemoved => "password-local-removed",
            EmailJobKind.IdentityPrincipalChanged => "identity-principal-changed",
            EmailJobKind.IdentityLinkedByAdmin => "identity-linked-by-admin",
            EmailJobKind.IdentityRemovedByAdmin => "identity-removed-by-admin",
            EmailJobKind.ProviderDisabled => "provider-disabled",
            EmailJobKind.ProviderEnabled => "provider-enabled",
            EmailJobKind.ProviderAuthorizationRevoked => "provider-authorization-revoked",
            EmailJobKind.ProviderAuthorizationGranted => "provider-authorization-granted",
            EmailJobKind.OAuthConsentExpired => "oauth-consent-expired",
            EmailJobKind.SessionRevoked => "session-revoked",
            EmailJobKind.SecurityNotification => "security-notification",
            _ => null
        };

        if (templateCode == null)
            return Result<EmailResult>.Failure("UNKNOWN_KIND", $"Tipo de email desconocido: {job.Kind}");

        var templateResult = await _templateRepo.ObtenerPorNombreCulturaAsync(templateCode, "es", job.IdTenant, ct);
        if (templateResult.IsFailure || templateResult.Value == null)
            return Result<EmailResult>.Failure("TEMPLATE_NOT_FOUND", $"Plantilla no encontrada: {templateCode}");
        var idTemplate = templateResult.Value.Id;

        var resolvedEmail = job.ToEmail;
        if (string.IsNullOrWhiteSpace(resolvedEmail) && job.IdUsuario.HasValue)
        {
            var userResult = await _usuarioRepo.ObtenerPorIdAsync(job.IdUsuario.Value, ct);
            if (userResult.IsSuccess && userResult.Value != null)
            {
                resolvedEmail = userResult.Value.Email ?? "";
                _logger.LogInformation("Email resuelto desde IdUsuario={IdUsuario}: {Email}", job.IdUsuario.Value, resolvedEmail);
            }
        }

        // If user has no email configured, log and skip sending (FASE 13: usuarios sin email)
        if (string.IsNullOrWhiteSpace(resolvedEmail))
        {
            _logger.LogInformation("Usuario sin Email configurado - Omitiendo envío de email tipo {Kind} para IdUsuario={IdUsuario}", job.Kind, job.IdUsuario);
            return Result<EmailResult>.Success(new EmailResult 
            { 
                Success = true, 
                TrackingId = $"skipped-{Guid.NewGuid():N}" 
            });
        }

        var userName = job.UserName;
        if (string.IsNullOrWhiteSpace(userName) && job.IdUsuario.HasValue)
        {
            var userResult = await _usuarioRepo.ObtenerPorIdAsync(job.IdUsuario.Value, ct);
            if (userResult.IsSuccess && userResult.Value != null)
                userName = userResult.Value.NomUsuario;
        }

        var vars = new Dictionary<string, object?>
        {
            ["UserName"] = userName,
            ["FechaHora"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        foreach (var kv in job.Extra)
            vars[kv.Key] = kv.Value;

        var subjectResult = await _templateStore.RenderSubjectAsync(templateCode, vars, ct: ct);
        if (subjectResult.IsFailure)
            return Result<EmailResult>.Failure(subjectResult.Error!);

        var bodyResult = await _templateStore.RenderBodyAsync(templateCode, vars, ct: ct);
        if (bodyResult.IsFailure)
            return Result<EmailResult>.Failure(bodyResult.Error!);

        var extraWithKind = new Dictionary<string, object?>(job.Extra)
        {
            ["__EmailJobKind"] = job.Kind.ToString()
        };
        var extraJson = JsonSerializer.Serialize(extraWithKind);

        return await SendEmailAsync(resolvedEmail, subjectResult.Value, bodyResult.Value, true, job.IdTenant, job.IdUsuario, job.IdApp, ct, job.EmailLogId, job.CorrelationId, extraJson, idTemplate);
    }

    private async Task<Result<EmailResult>> SendEmailAsync(
        string toEmail, string subject, string body, bool isHtml,
        int? idTenant, int? idUsuario, int? idApp, CancellationToken ct,
        long? existingLogId = null, string? correlationId = null, string? extraJson = null, int? idTemplate = null)
    {
        try
        {
            var accountResult = await _accountResolver.ResolveAsync(idApp, idTenant, ct);
            if (accountResult.IsFailure)
                return Result<EmailResult>.Failure(accountResult.Error!);

            var (account, smtpConfig) = accountResult.Value;

            var proveedor = account.Provider?.Nombre ?? account.Provider?.Codigo ?? account.Nombre;

            if (!IsValidEmailFormat(toEmail))
                return Result<EmailResult>.Failure("INVALID_EMAIL_FORMAT", $"Formato de email inválido: {toEmail}");

            var emailLog = await CreateOrUpdateLogAsync(existingLogId, account, toEmail, subject, null, idTenant, idUsuario, idApp, correlationId, ct, extraJson, idTemplate, proveedor);

            var emailSettings = new EmailSettings
            {
                DefaultFromEmail = smtpConfig.FromEmail,
                DefaultFromName = smtpConfig.FromName,
                SelectionStrategy = AccountSelectionStrategy.Priority
            };
            emailSettings.SmtpAccounts.Add(smtpConfig);

            var emailService = new EmailService(emailSettings);
            var message = new EmailMessage(toEmail, subject, body, isHtml);

            try
            {
                var result = await emailService.SendEmailAsync(message, ct);

                emailLog.Intentos = (byte)(emailLog.Intentos + 1);
                emailLog.FecUltIntento = DateTime.Now;

                if (result.Success)
                {
                    emailLog.Estado = "enviado";
                    emailLog.FecEnvio = DateTime.Now;
                    emailLog.MsgIdExterno = result.TrackingId;
                    var updateResult = _emailLogRepo.Update(emailLog);
                    await _uow.SaveChangesAsync(ct);

                    _olog.LogInformation(new LogEvent
                    {
                        EventName = LoggingEvents.EmailSent,
                        Scope = LoggingScopes.Email,
                        Message = "Email enviado",
                        Properties = new Dictionary<string, object?>
                        {
                            [LoggingPropertyNames.Category] = LoggingCategories.Application,
                            [LoggingPropertyNames.Operation] = LoggingOperations.Send,
                            [LoggingPropertyNames.CorrelationId] = correlationId,
                            [LoggingPropertyNames.UserId] = idUsuario?.ToString(),
                            [LoggingPropertyNames.TenantId] = idTenant,
                        }
                    });
                    return Result<EmailResult>.Success(result);
                }
                else
                {
                    emailLog.ErrorDetalle = result.ErrorMessage ?? "Error desconocido del proveedor";
                    emailLog.Estado = emailLog.Intentos >= 3 ? "fallido" : "pendiente";
                    var updateResult = _emailLogRepo.Update(emailLog);
                    await _uow.SaveChangesAsync(ct);

                    _olog.LogWarning(new LogEvent
                    {
                        EventName = LoggingEvents.EmailFailed,
                        Scope = LoggingScopes.Email,
                        Message = "Email falló",
                        Properties = new Dictionary<string, object?>
                        {
                            [LoggingPropertyNames.Category] = LoggingCategories.Application,
                            [LoggingPropertyNames.Operation] = LoggingOperations.Send,
                            [LoggingPropertyNames.CorrelationId] = correlationId,
                            [LoggingPropertyNames.UserId] = idUsuario?.ToString(),
                            [LoggingPropertyNames.TenantId] = idTenant,
                        }
                    });
                    return Result<EmailResult>.Failure("EMAIL_PROVIDER_ERROR", emailLog.ErrorDetalle);
                }
            }
            catch (Exception ex)
            {
                emailLog.Intentos = (byte)(emailLog.Intentos + 1);
                emailLog.FecUltIntento = DateTime.Now;
                emailLog.ErrorDetalle = ex.Message;
                emailLog.Estado = emailLog.Intentos >= 3 ? "fallido" : "pendiente";
                var updateResult = _emailLogRepo.Update(emailLog);
                await _uow.SaveChangesAsync(ct);

                _olog.LogError(new LogEvent
                {
                    EventName = LoggingEvents.EmailFailed,
                    Scope = LoggingScopes.Email,
                    Message = "Email falló por excepción",
                    Properties = new Dictionary<string, object?>
                    {
                        [LoggingPropertyNames.Category] = LoggingCategories.Application,
                        [LoggingPropertyNames.Operation] = LoggingOperations.Send,
                        [LoggingPropertyNames.CorrelationId] = correlationId,
                        [LoggingPropertyNames.UserId] = idUsuario?.ToString(),
                        [LoggingPropertyNames.TenantId] = idTenant,
                    }
                });
                return Result<EmailResult>.Failure("EMAIL_SEND_ERROR", $"Error al enviar email: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return Result<EmailResult>.Failure("EMAIL_SEND_ERROR", $"Error al enviar email: {ex.Message}");
        }
    }

    private async Task<EmailLog> CreateOrUpdateLogAsync(
        long? existingLogId, EmailAccount account, string toEmail, string subject, string? body,
        int? idTenant, int? idUsuario, int? idApp, string? correlationId, CancellationToken ct,
        string? extraJson = null, int? idTemplate = null, string? proveedor = null)
    {
        if (existingLogId.HasValue)
        {
            var existingResult = await _emailLogRepo.GetByIdAsync(existingLogId.Value, ct);
            if (existingResult.IsSuccess && existingResult.Value != null)
            {
                existingResult.Value.Intentos++;
                existingResult.Value.FecUltIntento = DateTime.Now;
                if (!string.IsNullOrEmpty(proveedor))
                    existingResult.Value.Proveedor = proveedor;
                return existingResult.Value;
            }
        }

        var emailLog = EmailLog.Crear(toEmail, subject, idTenant, idUsuario, idApp, idTemplate, account.Id, correlationId, extraJson, proveedor);
        var addResult = _emailLogRepo.Add(emailLog);
        await _uow.SaveChangesAsync(ct);

        return emailLog;
    }

    private static bool IsValidEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && addr.Host.Contains('.');
        }
        catch
        {
            return false;
        }
    }
}
