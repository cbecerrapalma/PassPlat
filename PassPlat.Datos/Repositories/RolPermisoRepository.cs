using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IRolPermisoRepository : IRepositoryAsync<RolPermiso>
{
    Task<Result<IReadOnlyList<RolPermiso>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default);
    Task<Result<RolPermiso?>> ObtenerActivoPorRolPermisoAsync(int idRol, int idPermiso, CancellationToken ct = default);
}

public class RolPermisoRepository : RepositoryAsync<RolPermiso>, IRolPermisoRepository
{
    public RolPermisoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<RolPermiso>>> ObtenerPermisosPorRolAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(rp => rp.IdRol == idRol && rp.Activo)
                .Include(rp => rp.Permiso)
                .ToListAsync(ct);
            return Result<IReadOnlyList<RolPermiso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RolPermiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<RolPermiso?>> ObtenerActivoPorRolPermisoAsync(int idRol, int idPermiso, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(rp => rp.IdRol == idRol && rp.IdPermiso == idPermiso && rp.Activo, ct);
            return Result<RolPermiso?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<RolPermiso?>.Failure("DB_ERROR", ex.Message);
        }
    }
}
