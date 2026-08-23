using CBP.Emails.Configuration;
using CBP.Results;
using CBP.Security.Cryptography.Services;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services.Email;

public interface IEmailAccountResolverService
{
    Task<Result<(EmailAccount Account, SmtpAccountConfig SmtpConfig)>> ResolveAsync(int? idApp, int? idTenant, CancellationToken ct = default);
}

public class EmailAccountResolverService : IEmailAccountResolverService
{
    private readonly IAppEmailAccountRepository _appEmailAccountRepo;
    private readonly ITenantEmailAccountRepository _tenantEmailAccountRepo;
    private readonly IEmailAccountRepository _emailAccountRepo;
    private readonly IEncryptionService _encryption;

    public EmailAccountResolverService(
        IAppEmailAccountRepository appEmailAccountRepo,
        ITenantEmailAccountRepository tenantEmailAccountRepo,
        IEmailAccountRepository emailAccountRepo,
        IEncryptionService encryption)
    {
        _appEmailAccountRepo = appEmailAccountRepo;
        _tenantEmailAccountRepo = tenantEmailAccountRepo;
        _emailAccountRepo = emailAccountRepo;
        _encryption = encryption;
    }

    public async Task<Result<(EmailAccount Account, SmtpAccountConfig SmtpConfig)>> ResolveAsync(int? idApp, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            // Priority 1: App-level default account
            if (idApp.HasValue)
            {
                var appAccountsResult = await _appEmailAccountRepo.ObtenerPorAppAsync(idApp.Value, ct);
                if (appAccountsResult.IsSuccess && appAccountsResult.Value?.Count > 0)
                {
                    var appAccount = appAccountsResult.Value
                        .OrderByDescending(aa => aa.EsPredeterminada)
                        .ThenBy(aa => aa.Id)
                        .First();

                    if (appAccount.EmailAccount != null && appAccount.EmailAccount.Activo)
                    {
                        var config = BuildSmtpConfig(appAccount.EmailAccount);
                        return Result<(EmailAccount, SmtpAccountConfig)>.Success((appAccount.EmailAccount, config));
                    }
                }
            }

            // Priority 2: Tenant-level default account
            if (idTenant.HasValue)
            {
                var tenantAccountsResult = await _tenantEmailAccountRepo.ObtenerPorTenantAsync(idTenant.Value, ct);
                if (tenantAccountsResult.IsSuccess && tenantAccountsResult.Value?.Count > 0)
                {
                    var tenantAccount = tenantAccountsResult.Value
                        .OrderByDescending(ta => ta.EsPredeterminada)
                        .ThenBy(ta => ta.Id)
                        .First();

                    if (tenantAccount.EmailAccount != null && tenantAccount.EmailAccount.Activo)
                    {
                        var config = BuildSmtpConfig(tenantAccount.EmailAccount);
                        return Result<(EmailAccount, SmtpAccountConfig)>.Success((tenantAccount.EmailAccount, config));
                    }
                }
            }

            // Priority 3: Global default account
            var globalResult = await _emailAccountRepo.ObtenerPredeterminadaAsync(ct);
            if (globalResult.IsSuccess && globalResult.Value != null)
            {
                var config = BuildSmtpConfig(globalResult.Value);
                return Result<(EmailAccount, SmtpAccountConfig)>.Success((globalResult.Value, config));
            }

            // Priority 4: First active global account
            var activosResult = await _emailAccountRepo.ObtenerActivosAsync(ct);
            if (activosResult.IsSuccess && activosResult.Value?.Count > 0)
            {
                var firstActive = activosResult.Value
                    .OrderBy(ea => ea.Id)
                    .First();

                var config = BuildSmtpConfig(firstActive);
                return Result<(EmailAccount, SmtpAccountConfig)>.Success((firstActive, config));
            }

            return Result<(EmailAccount, SmtpAccountConfig)>.Failure("EMAIL_NO_ACCOUNT", "No hay cuentas de email activas configuradas");
        }
        catch (Exception ex)
        {
            return Result<(EmailAccount, SmtpAccountConfig)>.Failure("EMAIL_RESOLVE_ERROR", $"Error al resolver cuenta de email: {ex.Message}");
        }
    }

    private SmtpAccountConfig BuildSmtpConfig(EmailAccount account)
    {
        var decryptedPassword = DecryptPassword(account);

        return new SmtpAccountConfig
        {
            Name = account.Nombre,
            Host = account.Host,
            Port = account.Puerto,
            UseSsl = account.UsaSSL || !account.UsaTLS,
            Username = account.SmtpUsuario,
            Password = decryptedPassword,
            FromEmail = account.FromAddress,
            FromName = account.FromName ?? account.FromAddress,
            IsEnabled = account.Activo,
            Priority = account.EsPredeterminada ? 0 : 1,
            TimeoutSeconds = 30
        };
    }

    private string DecryptPassword(EmailAccount account)
    {
        if (string.IsNullOrEmpty(account.Password))
            return "";

        try
        {
            return _encryption.Decrypt(account.Password, "EmailAccount");
        }
        catch (Exception)
        {
            return "";
        }
    }
}
