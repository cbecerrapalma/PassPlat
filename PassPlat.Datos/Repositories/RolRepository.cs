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
    Task<Result<IReadOnlyList<Rol>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default);
    // S111 · D2 — roles con al menos un permiso activo de un módulo asignado a alguna de las apps
    Task<Result<IReadOnlyList<Rol>>> ObtenerPorAppsAsync(int? idTenant, IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default);
    Task<Result<CrearRolResult>> CrearConSPAsync(int? idTenant, string codigo, string nombre, string? descripcion, int? idPolitica, string? idsPermisos, CancellationToken ct = default);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Rol?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Rol>>> ObtenerParaTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet.Include(r => r.Tenant).Where(r => (r.IdTenant == idTenant || r.IdTenant == null) && r.Activo).ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S111 · D2 — alcance de tenant (propio + globales) y pertenencia a la app por permiso de rol
    public async Task<Result<IReadOnlyList<Rol>>> ObtenerPorAppsAsync(int? idTenant, IReadOnlyCollection<int> idsApp, bool soloActivos, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(r => r.Tenant).AsNoTracking();
            if (idTenant.HasValue)
                query = query.Where(r => r.IdTenant == idTenant.Value || r.IdTenant == null);
            if (soloActivos)
                query = query.Where(r => r.Activo);
            query = query.Where(r => r.RolesPermisos.Any(rp => rp.Activo
                && rp.Permiso != null
                && rp.Permiso.Modulo != null
                && rp.Permiso.Modulo.AppsModulos.Any(am => am.Activo && idsApp.Contains(am.IdApp))));
            var list = await query.ToListAsync(ct);
            return Result<IReadOnlyList<Rol>>.Success(list);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<Rol>>.Failure("DB_ERROR", ex.Message);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<Rol> Items, int TotalCount)>.Failure("DB_ERROR", ex.Message);
        }
    }
}
