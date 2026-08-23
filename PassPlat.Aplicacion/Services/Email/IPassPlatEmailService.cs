using CBP.Emails.Core.Models;
using CBP.Results;

namespace PassPlat.Aplicacion.Services.Email;

public interface IPassPlatEmailService
{
    Task<Result<EmailResult>> SendPasswordResetAsync(string toEmail, string userName, string resetLink, CancellationToken ct = default);
    Task<Result<EmailResult>> SendMfaCodeAsync(string toEmail, string userName, string code, CancellationToken ct = default);
    Task<Result<EmailResult>> SendWelcomeAsync(string toEmail, string userName, string appName, string loginUrl, CancellationToken ct = default);
    Task<Result<EmailResult>> SendSecurityAlertAsync(string toEmail, string userName, string alertMessage, string ip, CancellationToken ct = default);
    Task<Result<EmailResult>> SendAccountLockedAsync(string toEmail, string userName, int minutes, CancellationToken ct = default);
    Task<Result<EmailResult>> SendPasswordChangedAsync(string toEmail, string userName, string tipoCambio, CancellationToken ct = default);
    Task<Result<EmailResult>> SendNotificationAsync(string templateCode, string toEmail, string userName, IReadOnlyDictionary<string, object?>? extraVariables = null, CancellationToken ct = default);
    Task<Result<EmailResult>> SendFromTemplateAsync(string templateCode, string toEmail, IReadOnlyDictionary<string, object?> variables, string? accountGroup = null, CancellationToken ct = default);
    Task<Result<EmailResult>> SendRawAsync(string toEmail, string subject, string body, bool isHtml = true, string? accountGroup = null, CancellationToken ct = default);
    Task<Result<EmailResult>> SendFromJobAsync(EmailJob job, CancellationToken ct = default);
    Task InvalidateCacheAsync(CancellationToken ct = default);
}
