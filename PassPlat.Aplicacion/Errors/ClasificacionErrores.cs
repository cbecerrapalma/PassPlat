using CBP.Results.Errors;

namespace PassPlat.Aplicacion.Errors;

/// <summary>
/// S64: clasificación central de los códigos de error de PassPlat.
/// <para>
/// Un <c>Result.Failure(código, mensaje)</c> sin tipo sale de la API como 500 (CBP
/// <c>ErrorExtensions</c>). El filtro de la WebAPI usa esta tabla para devolver el estado
/// correcto a los errores de negocio; los técnicos siguen en 500 a propósito.
/// </para>
/// <para>
/// Todo código usado en PassPlat.Aplicacion o PassPlat.Datos debe estar aquí, clasificado o
/// en <see cref="Tecnicos"/>; la prueba de arquitectura S64 falla si aparece uno nuevo.
/// </para>
/// </summary>
public static class ClasificacionErrores
{
    private static readonly Dictionary<string, ErrorType> Negocio = Construir(
        (ErrorType.NotFound,
        [
            "NO_ENCONTRADO", "NOT_FOUND", "USUARIO_NO_ENCONTRADO", "TEMPLATE_NOT_FOUND", "PROVIDER_NOT_FOUND",
            "DISP_NOT_FOUND", "IP_NOT_FOUND", "IDENEXT_NOT_FOUND", "ROL_NO_EXISTE", "NO_ASIGNADO",
            "PWD_NOT_FOUND", "POLITICA_NOT_FOUND", "NOTIF_NOT_FOUND", "MFA_NOT_FOUND",
            "MEMBRESIA_NO_ENCONTRADA", "ACCESO_NOT_FOUND", "CONFIG_NOT_FOUND",
            "EMAIL_PLANTILLA_NO_ENCONTRADA", "EMAIL_PLANTILLA_NO_PUBLICADA"
        ]),
        (ErrorType.Conflict,
        [
            "ACCESO_DUPLICADO", "CODIGO_DUPLICADO", "YA_EXISTE", "YA_ASIGNADO", "USUARIO_YA_ELIMINADO",
            "TENANT_SISTEMA", "ASIGNACION_DUPLICADA"
        ]),
        (ErrorType.Validation,
        [
            "LOGIN_VALIDATION", "INVALID_EMAIL_FORMAT", "ROL_INACTIVO", "PWD_POLICY_FAILED", "NOT_SUPPORTED",
            "NO_EMAIL", "MFA_SIN_EMAIL", "PROVIDER_INCOMPLETE_CONFIG", "PROVIDER_NOT_CONFIGURED",
            "PROVIDER_DISABLED", "PROVIDER_CONFIG_DISABLED", "PROVIDER_NOT_ENABLED_FOR_APP", "ROL_NO_ENCONTRADO", "ROL_OTRO_TENANT",
            "SIN_MEMBRESIA_TENANT", "DB_CONSTRAINT", "DATOS_INVALIDOS", "AUTOEDICION", "HERENCIA_CICLO",
            "APP_INVALIDA", "URL_RETORNO_NO_PERMITIDA", "CODIGO_INVALIDO", "MFA_REF_INVALIDA",
            "PASSWORD_WEAK", "PASSWORD_REUSE", "PWD_CHANGE_FAILED"
        ]),
        (ErrorType.Unauthorized,
        [
            "LOGIN_FAILED", "CredencialesInvalidas", "TOKEN_EXPIRED", "INVALID_REFRESH", "REFRESH_EXPIRED",
            "REFRESH_REUSE", "SESION_REVOCADA", "MFA_INVALIDO", "MFA_FALLIDO", "INVALID_CURRENT_PASSWORD",
            "REPLAY_DETECTED"
        ]),
        (ErrorType.Forbidden,
        [
            "FORBIDDEN", "SIN_ACCESO_PLATFORM", "SIN_ACCESO_TENANT", "SIN_ACCESO_APP", "CUENTA_INACTIVA",
            "CUENTA_ELIMINADA", "PWD_EXPIRADA", "TENANT_REQUIRED", "EMAIL_PLANTILLA_NO_PERMITIDA"
        ]));

    /// <summary>Códigos técnicos: siguen respondiendo 500.</summary>
    public static readonly IReadOnlySet<string> Tecnicos = new HashSet<string>(StringComparer.Ordinal)
    {
        "DB_ERROR", "SP_NO_RESULT", "NO_ROWS", "VALIDATION_ERROR", "KEY_NOT_FOUND", "UNKNOWN_KIND",
        "PWD_CREATION_FAILED", "APP_VALIDATION_ERROR", "TENANT_VALIDATION_ERROR",
        "PROVIDER_ERROR", "USERINFO_INVALIDO", "NOTIFY_ERROR", "EMAIL_NO_ACCOUNT", "EMAIL_PROVIDER_ERROR", "EMAIL_QUEUE_ERROR",
        "EMAIL_RESOLVE_ERROR", "EMAIL_SEND_ERROR", "MFA_READ_ERROR", "MFA_SEND_ERROR", "MFA_VALIDATE_ERROR",
        "REFRESH_ERROR", "SWITCH_ERROR", "SWITCH_PLATFORM_ERROR", "LOCK_ERROR", "LOCK_FAILED", "BG_ERROR",
        "LOGIN_ERROR", "DECRYPTION_ERROR", "SECRET_DECRYPT_ERROR", "TOKEN_PERSIST_ERROR"
    };

    /// <summary>Tipo de un código de negocio, o <c>null</c> si es técnico o desconocido (500).</summary>
    public static ErrorType? TipoPorCodigo(string? codigo) =>
        codigo is not null && Negocio.TryGetValue(codigo, out var tipo) ? tipo : null;

    /// <summary>Todos los códigos conocidos (de negocio y técnicos).</summary>
    public static IEnumerable<string> Conocidos => Negocio.Keys.Concat(Tecnicos);

    private static Dictionary<string, ErrorType> Construir(params (ErrorType Tipo, string[] Codigos)[] grupos)
    {
        var mapa = new Dictionary<string, ErrorType>(StringComparer.Ordinal);
        foreach (var (tipo, codigos) in grupos)
            foreach (var codigo in codigos)
                mapa.Add(codigo, tipo); // Add: un código repetido en dos grupos falla al iniciar
        return mapa;
    }
}
