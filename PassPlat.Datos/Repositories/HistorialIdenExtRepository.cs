using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IHistorialIdenExtRepository : IRepositoryAsync<HistorialIdenExt>
{
    Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorIdentidadAsync(long idIdenExt, CancellationToken ct = default);
    Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limit = 100, CancellationToken ct = default);
}

public class HistorialIdenExtRepository : RepositoryAsync<HistorialIdenExt>, IHistorialIdenExtRepository
{
    public HistorialIdenExtRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorIdentidadAsync(long idIdenExt, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(h => h.Usuario)
                .Include(h => h.ProvIden)
                .Include(h => h.RealizadoPorNav)
                .Where(h => h.IdIdenExt == idIdenExt)
                .OrderByDescending(h => h.FecCambio)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<HistorialIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HistorialIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(h => h.Usuario)
                .Include(h => h.ProvIden)
                .Include(h => h.RealizadoPorNav)
                .Where(h => h.IdUsuario == idUsuario)
                .OrderByDescending(h => h.FecCambio)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<HistorialIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HistorialIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<HistorialIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limit = 100, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(h => h.Usuario)
                .Include(h => h.ProvIden)
                .Include(h => h.RealizadoPorNav)
                .Where(h => h.IdTenant == idTenant)
                .OrderByDescending(h => h.FecCambio)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<HistorialIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HistorialIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
