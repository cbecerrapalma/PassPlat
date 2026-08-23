namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// Guard de gobernanza del catálogo de eventos de CBP.Logging.
/// Fuente de verdad: `CBP.Logging.LoggingEvents` (definición de las constantes),
/// derivada por reflexión. `Logging.EventCatalog.md` es DOCUMENTACIÓN sincronizada
/// (verificado por test T5B) y NUNCA fuente de autorización de eventos.
/// </summary>
public interface IEventNameCatalogGuard
{
    /// <summary>Catálogo valor→nombre derivado por reflexión de LoggingEvents.</summary>
    IReadOnlyDictionary<string, string> CatalogByValue { get; }

    /// <summary>
    /// Escanea todos los archivos .cs bajo <paramref name="root"/> (recursivo)
    /// y devuelve las violaciones detectadas: asignaciones `EventName = "literal"`
    /// donde el literal pertenece al catálogo. Los directorios obj/bin se excluyen.
    /// </summary>
    IReadOnlyList<EventNameLiteralViolation> Scan(string root, CancellationToken ct = default);

    /// <summary>Escanea un conjunto explícito de archivos .cs.</summary>
    IReadOnlyList<EventNameLiteralViolation> ScanFiles(IEnumerable<string> filePaths, CancellationToken ct = default);
}