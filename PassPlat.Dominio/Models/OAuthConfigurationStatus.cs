using PassPlat.Dominio.Enums;

namespace PassPlat.Dominio.Models;

public sealed record OAuthConfigurationStatus(
    bool IsConfigured,
    bool IsActive,
    bool HasErrors,
    IReadOnlyList<string> Errors,
    EEstadoConfiguracionOAuth Estado);
