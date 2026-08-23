using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using System.Text.Json;

namespace PassPlat.Datos.Repositories;

public interface IOutboxRepository
{
    Task<Result<int>> AddAsync(Outbox outbox, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Outbox>>> ObtenerPendientesAsync(int batchSize, CancellationToken ct = default);
    Task<Result<int>> MarcarProcessingAtomicAsync(long id, DateTime processingStartedAt, CancellationToken ct = default);
    Task<Result> MarcarPublishedAsync(long id, DateTime processedAt, CancellationToken ct = default);
    Task<Result> MarcarFailedAsync(long id, string error, DateTime nextAttempt, int attempts, CancellationToken ct = default);
    Task<Result> ReprogramarAsync(long id, DateTime nextAttempt, int attempts, CancellationToken ct = default);
    Task<Result> ResetStaleAsync(CancellationToken ct = default);
}

public class OutboxRepository : IOutboxRepository
{
    private readonly PassPlatDbContext _context;
    private readonly IUnitOfWorkAsync _uow;

    public OutboxRepository(PassPlatDbContext context, IUnitOfWorkAsync uow)
    {
        _context = context;
        _uow = uow;
    }

    public async Task<Result<int>> AddAsync(Outbox outbox, CancellationToken ct = default)
    {
        await _context.Outbox.AddAsync(outbox, ct);
        return Result<int>.Success(0);
    }

    public async Task<Result<IReadOnlyList<Outbox>>> ObtenerPendientesAsync(int batchSize, CancellationToken ct = default)
    {
        try
        {
            var list = await _context.Outbox
                .AsNoTracking()
                .Where(o => o.Status == "pending" && (o.NextAttemptAt == null || o.NextAttemptAt <= DateTime.UtcNow))
                .OrderBy(o => o.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Outbox>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Outbox>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> MarcarProcessingAtomicAsync(long id, DateTime processingStartedAt, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Outbox SET Status = 'processing', ProcessingStartedAt = {0}
                  WHERE Id = {1} AND Status = 'pending'",
                processingStartedAt, id);
            return Result<int>.Success(rows);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarPublishedAsync(long id, DateTime processedAt, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Outbox SET Status = 'published', ProcessingStartedAt = NULL, ProcessedAt = {0}
                  WHERE Id = {1}",
                processedAt, id);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarFailedAsync(long id, string error, DateTime nextAttempt, int attempts, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Outbox SET Status = 'failed', LastError = {0}, NextAttemptAt = {1}, Attempts = {2}
                  WHERE Id = {3}",
                error, nextAttempt, attempts, id);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> ReprogramarAsync(long id, DateTime nextAttempt, int attempts, CancellationToken ct = default)
    {
        try
        {
            var rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Outbox SET Status = 'pending', LastError = NULL, NextAttemptAt = {0}, Attempts = {1}
                  WHERE Id = {2}",
                nextAttempt, attempts, id);
            return rows > 0 ? Result.Success() : Result.Failure("NO_ROWS", $"Outbox id={id} not updated");
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> ResetStaleAsync(CancellationToken ct = default)
    {
        try
        {
            var staleBefore = DateTime.UtcNow.AddSeconds(-300);
            var rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Outbox SET Status = 'pending', ProcessingStartedAt = NULL
                  WHERE Status = 'processing' AND ProcessingStartedAt < {0}",
                staleBefore);
            return Result<int>.Success(rows);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }
}
