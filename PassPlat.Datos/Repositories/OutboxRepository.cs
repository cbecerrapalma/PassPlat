using CBP.Data.Abstractions;
using CBP.Outbox;
using CBP.Results;
using Microsoft.EntityFrameworkCore;

namespace PassPlat.Datos.Repositories;

// S136 · D8 — almacén del Outbox genérico de CBP sobre dbo.Outbox de PassPlat
public class OutboxRepository : IOutboxStore
{
    private readonly PassPlatDbContext _context;
    private readonly IUnitOfWorkAsync _uow;

    public OutboxRepository(PassPlatDbContext context, IUnitOfWorkAsync uow)
    {
        _context = context;
        _uow = uow;
    }

    public async Task<Result> VerificarAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.Outbox.AsNoTracking().Select(o => o.Id).Take(1).ToListAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    // La fila se guarda con el commit del llamador (un solo dueño del commit)
    public async Task<Result> AgregarAsync(OutboxMessage mensaje, CancellationToken ct = default)
    {
        try
        {
            await _context.Outbox.AddAsync(mensaje, ct);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<OutboxMessage>>> ObtenerPendientesAsync(int lote, DateTime ahora, CancellationToken ct = default)
    {
        try
        {
            var list = await _context.Outbox
                .AsNoTracking()
                .Where(o => o.Status == OutboxEstados.Pending && (o.NextAttemptAt == null || o.NextAttemptAt <= ahora))
                .OrderBy(o => o.CreatedAt)
                .Take(lote)
                .ToListAsync(ct);
            return Result<IReadOnlyList<OutboxMessage>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<OutboxMessage>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S96.1 · D3 — actualizaciones atómicas con ExecuteUpdateAsync (sin SQL en strings)
    public async Task<Result<bool>> ReclamarAsync(long id, DateTime ahora, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Id == id && o.Status == OutboxEstados.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OutboxEstados.Processing)
                    .SetProperty(o => o.ProcessingStartedAt, (DateTime?)ahora), ct);
            return Result<bool>.Success(rows > 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarPublicadoAsync(long id, DateTime ahora, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OutboxEstados.Published)
                    .SetProperty(o => o.ProcessingStartedAt, (DateTime?)null)
                    .SetProperty(o => o.ProcessedAt, (DateTime?)ahora), ct);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarFallidoAsync(long id, string error, DateTime ahora, int intentos, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OutboxEstados.Failed)
                    .SetProperty(o => o.LastError, error)
                    .SetProperty(o => o.NextAttemptAt, (DateTime?)ahora)
                    .SetProperty(o => o.Attempts, intentos), ct);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> ReprogramarAsync(long id, DateTime proximo, int intentos, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OutboxEstados.Pending)
                    .SetProperty(o => o.LastError, (string?)null)
                    .SetProperty(o => o.NextAttemptAt, (DateTime?)proximo)
                    .SetProperty(o => o.Attempts, intentos), ct);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    // S136 · D5 — el umbral lo calcula el procesador con ProcessingTimeoutSeconds (antes 300 s fijos)
    public async Task<Result<int>> LiberarAtascadosAsync(DateTime antesDe, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Status == OutboxEstados.Processing && o.ProcessingStartedAt < antesDe)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OutboxEstados.Pending)
                    .SetProperty(o => o.ProcessingStartedAt, (DateTime?)null), ct);
            return Result<int>.Success(rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> PurgarPublicadosAsync(DateTime antesDe, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Outbox
                .Where(o => o.Status == OutboxEstados.Published && o.ProcessedAt < antesDe)
                .ExecuteDeleteAsync(ct);
            return Result<int>.Success(rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }
}
