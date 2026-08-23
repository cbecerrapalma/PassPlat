using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using CBP.Data.Asynchronous;
using PassPlat.Datos.SPResults;

namespace PassPlat.Datos.Repositories;

public interface IRolRepository : IRepositoryAsync<Rol>
{
    Task<Result<IReadOnlyList<Rol>>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Rol>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Rol?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Rol>>> ObtenerGlobalesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Rol>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Rol?>> ObtenerPorIdConAccesosAsync(int idRol, CancellationToken ct = default);
    Task<Result<CrearRolResult>> CrearConSPAsync(int? idTenant, string codigo, string nombre, string? descripcion, int? idPolitica, string? idsPermisos, CancellationToken ct = default);
    Task<Result<ActualizarRolResult>> ActualizarConSPAsync(int idRol, string nombre, string? descripcion, bool activo, int? idUsrEjecutor, CancellationToken ct = default);
    Task<Result<ActualizarRolResult>> DesactivarConSPAsync(int idRol, int? idUsrEjecutor, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<Rol> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class RolRepository : RepositoryAsync<Rol>, IRolRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public RolRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow) : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<IReadOnlyList<Rol>>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(r => r.Tenant).ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Rol>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(r => r.Tenant).Where(r => r.IdTenant == idTenant && r.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Rol?>> ObtenerPorCodigoAsync(int idTenant, string codigo, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Include(r => r.Tenant).FirstOrDefaultAsync(r => r.IdTenant == idTenant && r.Codigo == codigo && r.Activo, ct);
            return Result<Rol?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Rol?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Rol>>> ObtenerGlobalesAsync(CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(r => r.Tenant).Where(r => r.IdTenant == null && r.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Rol>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(r => r.Tenant).Where(r => (r.IdTenant == idTenant || r.IdTenant == null) && r.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Rol?>> ObtenerPorIdConAccesosAsync(int idRol, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Include(r => r.Tenant).FirstOrDefaultAsync(r => r.Id == idRol && r.Activo, ct);
            return Result<Rol?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Rol?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<CrearRolResult>> CrearConSPAsync(int? idTenant, string codigo, string nombre, string? descripcion, int? idPolitica, string? idsPermisos, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.NVarChar("@Codigo", codigo, 20),
            RawParameter.NVarChar("@Nombre", nombre, 50),
            RawParameter.NVarChar("@Descripcion", descripcion, 200),
            RawParameter.Int("@IdPolitica", idPolitica),
            RawParameter.NVarChar("@IdsPermisos", idsPermisos, -1)
        };

        return await SpHelper.ExecuteSPAsync<CrearRolResult>(_rawQuery, "SP_Rol_Crear", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<ActualizarRolResult>> ActualizarConSPAsync(int idRol, string nombre, string? descripcion, bool activo, int? idUsrEjecutor, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdRol", idRol),
            RawParameter.NVarChar("@Nombre", nombre, 50),
            RawParameter.NVarChar("@Descripcion", descripcion, 200),
            RawParameter.Bit("@Activo", activo),
            RawParameter.Int("@IdUsrEjecutor", idUsrEjecutor)
        };

        return await SpHelper.ExecuteSPAsync<ActualizarRolResult>(_rawQuery, "SP_Rol_Actualizar", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<ActualizarRolResult>> DesactivarConSPAsync(int idRol, int? idUsrEjecutor, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdRol", idRol),
            RawParameter.Int("@IdUsrEjecutor", idUsrEjecutor)
        };

        return await SpHelper.ExecuteSPAsync<ActualizarRolResult>(_rawQuery, "SP_Rol_Desactivar", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<(IReadOnlyList<Rol> Items, int TotalCount)>> BuscarPaginadoAsync(string search, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            IQueryable<Rol> query = DbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Codigo.Contains(search) || r.Nombre.Contains(search));

            var total = await query.CountAsync(ct);
            var items = await query
                .Include(r => r.Tenant)
                .OrderBy(r => r.Nombre)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<Rol> Items, int TotalCount)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<Rol> Items, int TotalCount)>.Failure("DB_ERROR", ex.Message);
        }
    }
}
