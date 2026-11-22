using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IIdenExtTokensRepository : IRepositoryAsync<IdenExtTokens>
{
    Task<Result> MarcarTokenAnteriorInactivoAsync(long idIdenExt, int versionActual, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExtTokens>>> ObtenerTokensPorRenovarAsync(TimeSpan threshold, CancellationToken ct = default);
}

public class IdenExtTokensRepository : RepositoryAsync<IdenExtTokens>, IIdenExtTokensRepository
{
    public IdenExtTokensRepository(PassPlatDbContext context) : base(context) { }

    public async Task<Result> MarcarTokenAnteriorInactivoAsync(long idIdenExt, int versionActual, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.Now;
            foreach (var t in await DbSet
                .Where(t => t.IdIdenExt == idIdenExt && t.Activo && t.Version < versionActual)
                .ToListAsync(ct))
            {
                t.Activo = false;
                t.Revocado = true;
                t.FechaRevocacion = now;
                t.MotivoRevocacion = "Reemplazado por nueva versión";
            }
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IdenExtTokens>>> ObtenerTokensPorRenovarAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        try
        {
            var cutoff = DateTime.Now.Add(threshold);
            var list = await DbSet
                .Include(t => t.IdenExt)
                .ThenInclude(i => i!.ProvIden)
                .Where(t => t.Activo && !t.Revocado && t.RefreshTokenEnc != null && t.RefreshTokenExpires != null && t.RefreshTokenExpires < cutoff && t.RefreshTokenExpires > DateTime.Now)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<IdenExtTokens>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<IdenExtTokens>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
