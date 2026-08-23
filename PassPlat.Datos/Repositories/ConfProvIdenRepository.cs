using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IConfProvIdenRepository : IRepositoryAsync<ConfProvIden>
{
    Task<Result<ConfProvIden?>> ObtenerConfiguracionAsync(int idTenant, int idProvIden, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Rol?>> ObtenerRolDefectoAsync(int idRol, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerTodosAsync(CancellationToken ct = default);
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
                .FirstOrDefaultAsync(c => c.IdTenant == idTenant && c.IdProvIden == idProvIden && c.Activo, ct);
            return Result<ConfProvIden?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<ConfProvIden?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Rol?>> ObtenerRolDefectoAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var entity = await Context.Set<Rol>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == idRol, ct);
            return Result<Rol?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
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
                .Where(c => c.IdTenant == idTenant)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<ConfProvIden>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConfProvIden>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<ConfProvIden>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfProvIden>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
