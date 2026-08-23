using CBP.Results;

namespace PassPlat.Aplicacion.Services.Dashboard;

/// <summary>
/// Fuente de estado operacional mostrable en el Dashboard Enterprise.
/// La interfaz NO se limita a servicios que implementan <c>BackgroundService</c>:
/// también participan componentes de estado operativo como <c>EmailQueue</c>,
/// que no es un BackgroundService pero sí una fuente de estado de la plataforma.
/// </summary>
public interface IBackgroundJobStatus
{
    /// <summary>Nombre legible que se muestra en el Dashboard (ej: "EmailBackgroundService", "EmailQueue").</summary>
    string Nombre { get; }

    /// <summary>Lee el estado operativo actual de la fuente.</summary>
    Task<Result<BackgroundJobStatus>> ObtenerEstadoAsync(CancellationToken ct = default);
}

/// <summary>
/// Estado inmutable capturado de una fuente operacional del Dashboard.
/// </summary>
public readonly record struct BackgroundJobStatus(
    bool? Ejecutando,
    DateTime? UltimaEjecucion,
    int? ItemsProcesados,
    string? Detalle = null);