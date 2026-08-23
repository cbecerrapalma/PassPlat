namespace PassPlat.Dominio.Enums;

public enum EEstadoConfiguracionOAuth : byte
{
    NoConfigurado = 0,
    NoSoportado = 1,
    Inactivo = 2,
    Incompleto = 3,
    Configurado = 4,
    ConfiguracionInvalida = 5
}
