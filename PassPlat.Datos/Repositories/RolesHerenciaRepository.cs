using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Repositories;

public interface IRolesHerenciaRepository : IRepositoryAsync<RolesHerencia>
{
    Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerHijosAsync(int idRolPadre, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerPadresAsync(int idRolHijo, CancellationToken ct = default);
    Task<Result<RolesHerencia?>> ObtenerRelacionAsync(int idRolHijo, int idRolPadre, CancellationToken ct = default);
}

public class RolesHerenciaRepository : RepositoryAsync<RolesHerencia>, IRolesHerenciaRepository
{
    public RolesHerenciaRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(r => r.IdTenant == idTenant)
                .Include(r => r.RolHijo)
                .Include(r => r.RolPadre)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<RolesHerencia>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RolesHerencia>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerHijosAsync(int idRolPadre, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(r => r.IdRolPadre == idRolPadre && r.Activo)
                .Include(r => r.RolHijo)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<RolesHerencia>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RolesHerencia>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<RolesHerencia>>> ObtenerPadresAsync(int idRolHijo, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(r => r.IdRolHijo == idRolHijo && r.Activo)
                .Include(r => r.RolPadre)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<RolesHerencia>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RolesHerencia>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<RolesHerencia?>> ObtenerRelacionAsync(int idRolHijo, int idRolPadre, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(r => r.IdRolHijo == idRolHijo && r.IdRolPadre == idRolPadre, ct);
            return Result<RolesHerencia?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<RolesHerencia?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
