using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using CBP.Results.Errors;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IConfProvIdenRepository : IRepositoryAsync<ConfProvIden>
{
    Task<Result<ConfProvIden?>> ObtenerConfiguracionAsync(int idTenant, int idProvIden, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Rol?>> ObtenerRolDefectoAsync(int idRol, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<(IReadOnlyList<ConfProvIden> Items, int TotalCount)>> BuscarPaginadoAsync(int? idTenant, string? search, int pageNumber, int pageSize, CancellationToken ct = default);
    // S126 · D3 — con seguimiento y con Apps, para editar los enlaces
    Task<Result<ConfProvIden>> ObtenerConAppsAsync(int id, CancellationToken ct = default);
}

public class ConfProvIdenRepository : RepositoryAsync<ConfProvIden>, IConfProvIdenRepository
{
    public ConfProvIdenRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<ConfProvIden?>> ObtenerConfiguracionAsync(int idTenant, int idProvIden, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Include(c => c.ProvIden)
                .Include(c => c.RolDefectoNav)
                .Include(c => c.Apps)
                .FirstOrDefaultAsync(c => c.IdTenant == idTenant && c.IdProvIden == idProvIden && c.Activo, ct);
            return Result<ConfProvIden?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ConfProvIden?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<ConfProvIden>> ObtenerConAppsAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Include(c => c.Apps)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            return entity is null
                ? Result<ConfProvIden>.Failure(CommonErrors.NotFound.Entity("ConfProvIden", id))
                : Result<ConfProvIden>.Success(entity);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ConfProvIden>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Rol?>> ObtenerRolDefectoAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var entity = await Context.Set<Rol>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == idRol, ct);
            return Result<Rol?>.Success(entity, allowNull: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Rol?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(c => c.ProvIden)
                .Include(c => c.RolDefectoNav)
                .Include(c => c.Apps)
                .Where(c => c.IdTenant == idTenant)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<ConfProvIden>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<ConfProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S95 · D3 — lista paginada con búsqueda (antes la API ignoraba search); idTenant null = todos
    public async Task<Result<(IReadOnlyList<ConfProvIden> Items, int TotalCount)>> BuscarPaginadoAsync(int? idTenant, string? search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            IQueryable<ConfProvIden> query = DbSet.AsNoTracking()
                .Include(c => c.ProvIden)
                .Include(c => c.Tenant)
                .Include(c => c.RolDefectoNav)
                .Include(c => c.Apps);

            if (idTenant.HasValue)
                query = query.Where(c => c.IdTenant == idTenant.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var texto = search.Trim();
                query = query.Where(c =>
                    c.ClientId.Contains(texto) ||
                    (c.ProvIden != null && c.ProvIden.Nombre.Contains(texto)) ||
                    (c.Tenant != null && c.Tenant.Nombre.Contains(texto)));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(c => c.IdTenant)
                .ThenBy(c => c.IdProvIden)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<ConfProvIden>, int)>.Success((items, total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<ConfProvIden>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<ConfProvIden>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<ConfProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
