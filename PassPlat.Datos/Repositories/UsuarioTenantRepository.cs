using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IUsuarioTenantRepository : IRepositoryAsync<UsuarioTenant>
{
    Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<UsuarioTenant?>> ObtenerMembresiaAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerActivosPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<UsuarioTenant?>> ObtenerActivoPorTenantAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<bool>> ExisteMembresiaAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<int>> ResolverIdUsuarioTenantAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<int>>> ObtenerIdsUsuariosActivosPorTenantAsync(int idTenant, CancellationToken ct = default);
}

public class UsuarioTenantRepository : RepositoryAsync<UsuarioTenant>, IUsuarioTenantRepository
{
    public UsuarioTenantRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var items = await DbSet
                .Where(ut => ut.IdUsuario == idUsuario)
                .Include(ut => ut.Tenant)
                .Include(ut => ut.Estado)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<UsuarioTenant>>.Success(items);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<UsuarioTenant>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<UsuarioTenant?>> ObtenerMembresiaAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Include(ut => ut.Tenant)
                .Include(ut => ut.Estado)
                .FirstOrDefaultAsync(ut => ut.IdUsuario == idUsuario && ut.IdTenant == idTenant, ct);
            return Result<UsuarioTenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<UsuarioTenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<UsuarioTenant>>> ObtenerActivosPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var items = await DbSet
                .Where(ut => ut.IdUsuario == idUsuario && ut.Activo)
                .Include(ut => ut.Tenant)
                .Include(ut => ut.Estado)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<UsuarioTenant>>.Success(items);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<UsuarioTenant>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<UsuarioTenant?>> ObtenerActivoPorTenantAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Include(ut => ut.Tenant)
                .Include(ut => ut.Estado)
                .FirstOrDefaultAsync(ut => ut.IdUsuario == idUsuario && ut.IdTenant == idTenant && ut.Activo, ct);
            return Result<UsuarioTenant?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<UsuarioTenant?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> ExisteMembresiaAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var exists = await DbSet.AnyAsync(ut => ut.IdUsuario == idUsuario && ut.IdTenant == idTenant, ct);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ResolverIdUsuarioTenantAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var id = await DbSet
                .Where(ut => ut.IdUsuario == idUsuario && ut.IdTenant == idTenant)
                .Select(ut => (int?)ut.Id)
                .FirstOrDefaultAsync(ct);
            if (id == null)
                return Result<int>.Failure("MEMBRESIA_NO_ENCONTRADA", $"No se encontró membresía para usuario {idUsuario} en tenant {idTenant}");
            return Result<int>.Success(id.Value);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<int>>> ObtenerIdsUsuariosActivosPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var ids = await DbSet
                .Where(ut => ut.IdTenant == idTenant && ut.Activo)
                .Select(ut => ut.IdUsuario)
                .ToListAsync(ct);
            return Result<IReadOnlyList<int>>.Success(ids);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<int>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
