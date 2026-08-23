using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IAccesoRepository : IRepositoryAsync<Acceso>
{
    Task<Result<bool>> TieneAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosPorRolAsync(int idRol, CancellationToken ct = default);
    Task<Result<Acceso>> AsignarAccesoAsync(int idUsuario, int idTenant, int idApp, int idRol, CancellationToken ct = default);
    Task<Result<Acceso>> AsignarAccesoAsync(int idUsuario, int idTenant, int idApp, int idRol, int? idUsuarioTenant, CancellationToken ct = default);
    Result RevocarAcceso(int idUsuario, int idApp);
    Task<Result<IReadOnlyList<Acceso>>> ObtenerPlatformScopeAsync(int idUsuario, CancellationToken ct = default);
}

public class AccesoRepository : RepositoryAsync<Acceso>, IAccesoRepository
{
    public AccesoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<bool>> TieneAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.AnyAsync(a => a.IdUsuario == idUsuario && a.IdApp == idApp && a.Activo, ct);
            return Result<bool>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(a => a.IdUsuario == idUsuario && a.Activo)
                .Include(a => a.Usuario).Include(a => a.App).Include(a => a.Rol).Include(a => a.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Estado)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Acceso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Acceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosPorTenantYAppAsync(int idTenant, int idApp, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(a => a.IdTenant == idTenant && a.IdApp == idApp && a.Activo)
                .Include(a => a.Usuario).Include(a => a.App).Include(a => a.Rol).Include(a => a.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Estado)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Acceso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Acceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Acceso>>> ObtenerAccesosPorRolAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Where(a => a.IdRol == idRol && a.Activo)
                .Include(a => a.Usuario).Include(a => a.App).Include(a => a.Rol).Include(a => a.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Tenant)
                .Include(a => a.UsuarioTenant).ThenInclude(ut => ut!.Estado)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Acceso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Acceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Acceso>> AsignarAccesoAsync(int idUsuario, int idTenant, int idApp, int idRol, CancellationToken ct = default)
        => await AsignarAccesoAsync(idUsuario, idTenant, idApp, idRol, idUsuarioTenant: null, ct);

    public async Task<Result<Acceso>> AsignarAccesoAsync(int idUsuario, int idTenant, int idApp, int idRol, int? idUsuarioTenant, CancellationToken ct = default)
    {
        try
        {
            var existente = await DbSet.FirstOrDefaultAsync(
                a => a.IdUsuario == idUsuario && a.IdApp == idApp, ct);

            if (existente is not null)
            {
                existente.IdRol = idRol;
                existente.IdTenant = idTenant;
                existente.IdUsuarioTenant = idUsuarioTenant;
                if (!existente.Activo) existente.Activar();
                return Result<Acceso>.Success(existente);
            }

            var acceso = new Acceso
            {
                IdUsuario = idUsuario,
                IdTenant = idTenant,
                IdApp = idApp,
                IdRol = idRol,
                IdUsuarioTenant = idUsuarioTenant,
                Activo = true,
                FecAsignacion = DateTime.Now
            };
            DbSet.Add(acceso);
            return Result<Acceso>.Success(acceso);
        }
        catch (Exception ex)
        {
            return Result<Acceso>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result RevocarAcceso(int idUsuario, int idApp)
    {
        try
        {
            var acceso = DbSet.FirstOrDefault(a => a.IdUsuario == idUsuario && a.IdApp == idApp && a.Activo);
            if (acceso == null)
                return Result.Failure("ACCESO_NOT_FOUND", "Acceso no encontrado o ya revocado");

            acceso.Revocar();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Acceso>>> ObtenerPlatformScopeAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Where(a => a.IdUsuario == idUsuario && a.Activo && a.IdUsuarioTenant == null)
                .Include(a => a.App).Include(a => a.Rol)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<Acceso>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Acceso>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
