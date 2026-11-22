using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IHistorialPwdRepository : IRepositoryAsync<HistorialPwd>
{
    Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<HistorialPwd> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class HistorialPwdRepository : RepositoryAsync<HistorialPwd>, IHistorialPwdRepository
{
    public HistorialPwdRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Include(h => h.Politica).Where(h => h.IdUsuario == idUsuario).OrderByDescending(h => h.FecRegistro).Take(cantidad).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<HistorialPwd>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<HistorialPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<HistorialPwd> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(h => h.Usuario).Where(h => h.Usuario != null && h.Usuario.IdTenant == idTenant).AsNoTracking();
            var totalCount = await query.CountAsync(ct);
            var items = await query.OrderByDescending(h => h.FecRegistro)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Result<(IReadOnlyList<HistorialPwd>, int)>.Success((items, totalCount));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<HistorialPwd>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }
}