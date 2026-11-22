using CBP.Emails.Core.Models;
using CBP.Results;

namespace PassPlat.Aplicacion.Services.Email;

public interface IPassPlatEmailService
{
    Task<Result<EmailResult>> SendFromJobAsync(EmailJob job, CancellationToken ct = default);
    Task InvalidateCacheAsync(CancellationToken ct = default);
}
