using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Enums;
using PassPlat.Dominio.Models;

namespace PassPlat.Aplicacion.Services.OAuth;

public static class OAuthConfigurationStatusCalculator
{
    public static OAuthConfigurationStatus Calculate(ProvIden proveedor, ConfProvIden? configuracion)
    {
        if (configuracion is null)
            return new OAuthConfigurationStatus(false, false, false, [],
                EEstadoConfiguracionOAuth.NoConfigurado);

        if (!configuracion.Activo)
            return new OAuthConfigurationStatus(false, false, false, [],
                EEstadoConfiguracionOAuth.Inactivo);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(configuracion.ClientId))
            errors.Add("ClientId es requerido");

        if (string.IsNullOrWhiteSpace(configuracion.ClientSecret))
            errors.Add("ClientSecret es requerido");

        if (string.IsNullOrWhiteSpace(configuracion.Callback))
            errors.Add("Callback es requerido");
        else if (!configuracion.Callback.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add("Callback debe ser HTTPS");

        if (errors.Count > 0)
            return new OAuthConfigurationStatus(false, true, true, errors.AsReadOnly(),
                EEstadoConfiguracionOAuth.ConfiguracionInvalida);

        return new OAuthConfigurationStatus(true, true, false, [],
            EEstadoConfiguracionOAuth.Configurado);

        // Nota: NoSoportado se reserva para feature flags futuros.
        // Se asignará desde el Calculator solo cuando un proveedor esté
        // explícitamente deshabilitado por licencia/configuración global.
    }
}
