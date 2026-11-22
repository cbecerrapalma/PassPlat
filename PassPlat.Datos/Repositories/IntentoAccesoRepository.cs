using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IIntentoAccesoRepository : IRepositoryAsync<IntentoAcceso>
{
    Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>> GetPagedWithIncludesAsync(int pageNumber, int pageSize, int? idTenant = null, bool includeTotal = true, CancellationToken ct = default);
}

public class IntentoAccesoRepository : RepositoryAsync<IntentoAcceso>, IIntentoAccesoRepository
{
    public IntentoAccesoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>> GetPagedWithIncludesAsync(
        int pageNumber, int pageSize, int? idTenant = null, bool includeTotal = true, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(i => i.Resultado).Include(i => i.DireccionIP)
                .AsNoTracking()
                .AsQueryable();
            if (idTenant.HasValue)
                query = query.Where(i => i.IdTenant == idTenant.Value);
            query = query.OrderByDescending(i => i.FecIntento);
            var total = includeTotal ? await query.CountAsync(ct) : 0;
            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>.Success((items, total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            var result = await DbSet.Include(i => i.Resultado).Include(i => i.DireccionIP)
                .Where(i => i.IdUsuario == idUsuario && i.FecIntento >= desde)
                .OrderByDescending(i => i.FecIntento).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<IntentoAcceso>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<IntentoAcceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            return Result<int>.Success(await DbSet.CountAsync(i => i.IdUsuario == idUsuario && !i.Exitoso && i.FecIntento >= desde, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }
}
