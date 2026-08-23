namespace PassPlat.Aplicacion.Services.Dashboard;

/// <summary>
/// Estado mutable thread-safe que cada BackgroundService posee como instancia propia
/// para exponer su estado operativo al Dashboard. El estado pertenece al mismo objeto
/// que realmente ejecuta el ciclo (principio de identity DI: la instancia que corre es
/// la misma que reporta). Las escrituras son no-bloqueantes para el ciclo del job:
/// solo marcan flags/contadores, nunca alteran cancelación, retries o shutdown.
/// </summary>
public sealed class BackgroundJobState
{
    private readonly object _gate = new();
    private bool _ejecutando;
    private DateTime? _ultimaEjecucion;
    private int _itemsProcesados;

    /// <summary>Marca el job como en ejecución. No altera el flujo del ciclo.</summary>
    public void MarcarEjecutando()
    {
        lock (_gate)
        {
            _ejecutando = true;
        }
    }

    /// <summary>
    /// Marca el job como detenido (finalización del ciclo). Registra la última
    /// ejecución. No altera el flujo del ciclo.
    /// </summary>
    public void MarcarDetenido()
    {
        lock (_gate)
        {
            _ejecutando = false;
            _ultimaEjecucion = DateTime.Now;
        }
    }

    /// <summary>
    /// Acumula ítems procesados y registra la fecha de la última ejecución de trabajo
    /// real. No altera el flujo del ciclo.
    /// </summary>
    public void AgregarProcesados(int cantidad)
    {
        lock (_gate)
        {
            _itemsProcesados += cantidad;
            _ultimaEjecucion = DateTime.Now;
        }
    }

    /// <summary>
    /// Registra la última ejecución del ciclo del job (incluso sin ítems procesados)
    /// para que el Dashboard tenga evidencia honesta de que el job sigue ejecutándose.
    /// No altera el flujo del ciclo.
    /// </summary>
    public void RegistrarCiclo()
    {
        lock (_gate)
        {
            _ultimaEjecucion = DateTime.Now;
        }
    }

    /// <summary>Captura una fotografía inmutable del estado actual.</summary>
    public BackgroundJobStatus Snapshot() => Snapshot(null);

    /// <summary>
    /// Captura una fotografía del estado con detalle adicional opcional.
    /// </summary>
    public BackgroundJobStatus Snapshot(string? detalle)
    {
        lock (_gate)
        {
            return new BackgroundJobStatus(
                _ejecutando,
                _ultimaEjecucion,
                _itemsProcesados,
                detalle);
        }
    }
}