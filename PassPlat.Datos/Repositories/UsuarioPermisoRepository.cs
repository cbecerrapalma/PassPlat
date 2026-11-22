using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IUsuarioPermisoRepository : IRepositoryAsync<UsuarioPermiso>
{
    Task<Result<IReadOnlyList<UsuarioPermiso>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioPermiso>>> ObtenerActivosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default);
}

public class UsuarioPermisoRepository : RepositoryAsync<UsuarioPermiso>, IUsuarioPermisoRepository
{
    public UsuarioPermisoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<UsuarioPermiso>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(up => up.IdUsuario == idUsuario)
                .Include(up => up.Permiso)
                .Include(up => up.Tenant)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<UsuarioPermiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<UsuarioPermiso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<UsuarioPermiso>>> ObtenerActivosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(up => up.IdUsuario == idUsuario && up.IdTenant == idTenant && up.Activo)
                .Include(up => up.Permiso)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<UsuarioPermiso>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<UsuarioPermiso>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
