using CBP.Data.Abstractions;
using PassPlat.Datos;

namespace PassPlat.Aplicacion.Tests.Tests.S61;

/// <summary>Unidad de trabajo de prueba: persiste de verdad en el DbContext InMemory.</summary>
internal sealed class FakeUnitOfWork(PassPlatDbContext ctx) : IUnitOfWorkAsync
{
    public IRawQueryRepositoryAsync RawQuery => null!;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => ctx.SaveChangesAsync(ct);

    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        => ctx.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
        => await ctx.SaveChangesAsync(ct) > 0;

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default) => operation();

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default)
        => operation();

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
