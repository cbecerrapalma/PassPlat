using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;

namespace PassPlat.Aplicacion.Services.Dashboard;

public interface IBackgroundStatusService
{
    Task<Result<List<BackgroundJobDto>>> GetBackgroundJobsAsync(CancellationToken ct = default);
}

public class BackgroundStatusService : IBackgroundStatusService
{
    private readonly IEnumerable<IBackgroundJobStatus> _sources;
    private readonly ILogger<BackgroundStatusService> _logger;

    public BackgroundStatusService(
        IEnumerable<IBackgroundJobStatus> sources,
        ILogger<BackgroundStatusService> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    public async Task<Result<List<BackgroundJobDto>>> GetBackgroundJobsAsync(CancellationToken ct = default)
    {
        try
        {
            var jobs = new List<BackgroundJobDto>();

            foreach (var source in _sources)
            {
                BackgroundJobDto row;
                try
                {
                    var statusResult = await source.ObtenerEstadoAsync(ct);
                    if (statusResult.IsFailure)
                    {
                        row = new BackgroundJobDto
                        {
                            Nombre = source.Nombre,
                            Estado = "No disponible",
                            UltimaEjecucion = null,
                            ItemsProcesados = 0
                        };
                    }
                    else
                    {
                        var status = statusResult.Value;
                        row = new BackgroundJobDto
                        {
                            Nombre = source.Nombre,
                            Estado = status.Ejecutando switch
                            {
                                true => "Activo",
                                false => "Detenido",
                                null => "No disponible"
                            },
                            UltimaEjecucion = status.UltimaEjecucion,
                            ItemsProcesados = status.ItemsProcesados ?? 0
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo obtener estado de {Source}", source.Nombre);
                    row = new BackgroundJobDto
                    {
                        Nombre = source.Nombre,
                        Estado = "No disponible",
                        UltimaEjecucion = null,
                        ItemsProcesados = 0
                    };
                }

                jobs.Add(row);
            }

            return Result<List<BackgroundJobDto>>.Success(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar estado de background services");
            return Result<List<BackgroundJobDto>>.Failure("BG_ERROR", ex.Message);
        }
    }
}
