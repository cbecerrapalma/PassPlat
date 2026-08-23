using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IGrupoRepository : IRepositoryAsync<Grupo>
{
    Task<Result<IReadOnlyList<Grupo>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Grupo>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Grupo?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default);
}

public class GrupoRepository : RepositoryAsync<Grupo>, IGrupoRepository
{
    public GrupoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<Grupo>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(g => g.Tenant).OrderBy(g => g.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Grupo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Grupo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Grupo>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(g => g.IdTenant == idTenant).OrderBy(g => g.Nombre).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Grupo>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Grupo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Grupo?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(g => g.IdTenant == idTenant && g.Codigo == codigo, ct);
            return Result<Grupo?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Grupo?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
