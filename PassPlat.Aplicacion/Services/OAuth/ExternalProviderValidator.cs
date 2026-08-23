using CBP.Security.Cryptography.Services;
using Microsoft.Extensions.Logging;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Services.OAuth;

public class ProviderAvailabilityResult
{
    public bool Disponible { get; init; }
    public string? Motivo { get; init; }
}

public interface IExternalProviderValidator
{
    ProviderAvailabilityResult Validar(ConfProvIden config);
}

public class ExternalProviderValidator : IExternalProviderValidator
{
    private readonly IEncryptionService _encryption;
    private readonly ILogger<ExternalProviderValidator> _logger;

    public ExternalProviderValidator(IEncryptionService encryption, ILogger<ExternalProviderValidator> logger)
    {
        _encryption = encryption;
        _logger = logger;
    }

    public ProviderAvailabilityResult Validar(ConfProvIden config)
    {
        var providerCode = config.ProvIden?.Codigo ?? "(sin codigo)";
        var decryptOk = !string.IsNullOrWhiteSpace(config.ClientSecret) && _encryption.TryDecrypt(config.ClientSecret, out _, "ConfProvIden");

        if (!config.Activo)
            return LogNoDisponible(config, providerCode, "Proveedor inactivo");
        if (config.Estado != 1)
            return LogNoDisponible(config, providerCode, "Estado del proveedor no es válido");
        if (config.ProvIden is null || !config.ProvIden.Activo)
            return LogNoDisponible(config, providerCode, "Proveedor de identidad inactivo o no configurado");
        if (string.IsNullOrWhiteSpace(config.ProvIden.Codigo))
            return LogNoDisponible(config, providerCode, "Código de proveedor no configurado");
        if (string.IsNullOrWhiteSpace(config.ClientId) || config.ClientId.Trim().Length == 0)
            return LogNoDisponible(config, providerCode, "ClientId no configurado");
        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            return LogNoDisponible(config, providerCode, "ClientSecret no configurado");
        if (!decryptOk)
            return LogNoDisponible(config, providerCode, "ClientSecret cifrado inválido o corrupto");
        if (string.IsNullOrWhiteSpace(config.Callback) || !Uri.IsWellFormedUriString(config.Callback, UriKind.Absolute))
            return LogNoDisponible(config, providerCode, "Callback URI inválida");
        if (config.RolDefectoNav is null || !config.RolDefectoNav.Activo)
            return LogNoDisponible(config, providerCode, "Rol por defecto inactivo o no configurado");

        _logger.LogInformation(
            "OAuthProviderValidation {Provider} ACCEPTED | IdConfProvIden={IdConf} | TenantId={TenantId}",
            providerCode, config.Id, config.IdTenant);

        return new ProviderAvailabilityResult { Disponible = true };
    }

    private ProviderAvailabilityResult LogNoDisponible(ConfProvIden config, string providerCode, string motivo)
    {
        _logger.LogInformation(
            "OAuthProviderValidation {Provider} REJECTED | IdConfProvIden={IdConf} | TenantId={TenantId} | Motivo={Motivo} | " +
            "Activo={Activo} | Estado={Estado} | ProvIdenActivo={ProvAct} | ClientId={Cid} | ClientSecret={Cs} | " +
            "TryDecrypt={Decrypt} | Callback={Cb} | RolDefecto={Rol}",
            providerCode, config.Id, config.IdTenant, motivo,
            config.Activo, config.Estado == 1, config.ProvIden?.Activo,
            !string.IsNullOrWhiteSpace(config.ClientId),
            !string.IsNullOrWhiteSpace(config.ClientSecret),
            !string.IsNullOrWhiteSpace(config.ClientSecret) && _encryption.TryDecrypt(config.ClientSecret, out _, "ConfProvIden"),
            !string.IsNullOrWhiteSpace(config.Callback) && Uri.IsWellFormedUriString(config.Callback, UriKind.Absolute),
            config.RolDefectoNav != null && config.RolDefectoNav.Activo);

        return new ProviderAvailabilityResult { Disponible = false, Motivo = motivo };
    }
}
