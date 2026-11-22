using CBP.Events;
using CBP.Outbox;
using CBP.Results;
using Microsoft.Extensions.Logging;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Services.Security;

// S136 · D8 — deduplicación de NewIpDetectedEvent contra EmailLog (antes en OutboxProcessor.PublishEventAsync)
public sealed class NuevaIpOutboxFiltro : IOutboxPublicacionFiltro
{
    private readonly IEmailLogRepository _emailLogRepo;
    private readonly ILogger<NuevaIpOutboxFiltro> _logger;

    public NuevaIpOutboxFiltro(IEmailLogRepository emailLogRepo, ILogger<NuevaIpOutboxFiltro> logger)
    {
        _emailLogRepo = emailLogRepo;
        _logger = logger;
    }

    public async Task<Result<bool>> DebePublicarAsync(OutboxMessage mensaje, ICBPEvent evento, CancellationToken ct = default)
    {
        if (evento is not NewIpDetectedEvent nuevaIp)
            return Result<bool>.Success(true);

        var dedup = await _emailLogRepo.ExisteNotificacionNuevaIpAsync(nuevaIp.IdUsuario, nuevaIp.DireccionIP, ct);
        if (dedup.IsFailure)
        {
            LogSafe(() => _logger.LogWarning("No se pudo verificar dedup de NewIp: {Error}", dedup.Error?.Message));
            return Result<bool>.Failure(dedup.Error!);
        }

        if (dedup.Value)
        {
            LogSafe(() => _logger.LogInformation("NewIp dedup: notificacion ya existe para usuario {IdUsuario} IP {Ip} - omitiendo publicacion",
                nuevaIp.IdUsuario, nuevaIp.DireccionIP));
            return Result<bool>.Success(false);
        }

        return Result<bool>.Success(true);
    }

    private static void LogSafe(Action logAction)
    {
        try { logAction(); } catch { }
    }
}
