using CBP.Results;

namespace PassPlat.Aplicacion.Services;

public interface IDistributedLockService
{
    Task<Result<IAsyncDisposable>> AcquireLockAsync(string lockName, TimeSpan? timeout = null, CancellationToken ct = default);
}
