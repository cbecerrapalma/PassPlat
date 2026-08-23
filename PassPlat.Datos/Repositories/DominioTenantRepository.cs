using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IDominioTenantRepository : IRepositoryAsync<DominioTenant>
{
    Task<Result<IReadOnlyList<DominioTenant>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<DominioTenant?>> ObtenerPorDominioAsync(string dominio, CancellationToken ct = default);
    Task<Result<bool>> ExisteDominioAsync(int idTenant, string dominio, CancellationToken ct = default);
    Result<DominioTenant> AgregarDominio(int idTenant, string dominio);
    Result Actualizar(DominioTenant entity);
    Result Eliminar(DominioTenant entity);
}

public class DominioTenantRepository : RepositoryAsync<DominioTenant>, IDominioTenantRepository
{
    public DominioTenantRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<DominioTenant>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(d => d.IdTenant == idTenant).ToListAsync(ct);
            return Result<IReadOnlyList<DominioTenant>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DominioTenant>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<DominioTenant?>> ObtenerPorDominioAsync(string dominio, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Dominio == dominio, ct);
            return Result<DominioTenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<DominioTenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> ExisteDominioAsync(int idTenant, string dominio, CancellationToken ct = default)
    {
        try
        {
            var existe = await DbSet.AnyAsync(d => d.IdTenant == idTenant && d.Dominio == dominio, ct);
            return Result<bool>.Success(existe);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<DominioTenant> AgregarDominio(int idTenant, string dominio)
    {
        try
        {
            var dominioTenant = new DominioTenant { IdTenant = idTenant, Dominio = dominio };
            DbSet.Add(dominioTenant);
            return Result<DominioTenant>.Success(dominioTenant);
        }
        catch (Exception ex)
        {
            return Result<DominioTenant>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result Actualizar(DominioTenant entity)
    {
        try
        {
            Update(entity);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result Eliminar(DominioTenant entity)
    {
        try
        {
            Remove(entity);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}
