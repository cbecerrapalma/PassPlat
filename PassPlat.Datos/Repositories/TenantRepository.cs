using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface ITenantRepository : IRepositoryAsync<Tenant>
{
    Task<Result<Tenant?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Tenant>>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<Result<int>> CountActivosAsync(CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Tenant> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class TenantRepository : RepositoryAsync<Tenant>, ITenantRepository
{
    public TenantRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<Tenant?>> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(t => t.Codigo == codigo, ct);
            return Result<Tenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Tenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Tenant>>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var items = await DbSet.Where(t => t.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<Tenant>>.Success(items);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Tenant>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> CountActivosAsync(CancellationToken ct = default)
    {
        try
        {
            var count = await DbSet.CountAsync(t => t.Activo, ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<Tenant> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.Codigo.Contains(search) || t.Nombre.Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(t => t.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<Tenant> Items, int TotalCount)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<Tenant> Items, int TotalCount)>.Failure("DB_ERROR", ex.Message);
        }
    }
}
