using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Datos.Models;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IIdenExtRepository : IRepositoryAsync<IdenExt>
{
    Task<Result<IdenExt?>> ObtenerPorSubExternoAsync(int idProvIden, string subExterno, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorEstadoAsync(byte idEstado, CancellationToken ct = default);
    Task<Result<IPagedResult<IdenExt>>> BuscarAsync(BuscarIdenExtRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DesgloseProveedorReadModel>>> ObtenerDesglosePorProveedorAsync(int idTenant, CancellationToken ct = default);
}

public class IdenExtRepository : RepositoryAsync<IdenExt>, IIdenExtRepository
{
    public IdenExtRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IdenExt?>> ObtenerPorSubExternoAsync(int idProvIden, string subExterno, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .Include(i => i.ProvIden)
                .FirstOrDefaultAsync(i => i.IdProvIden == idProvIden && i.SubExterno == subExterno && !i.Eliminado, ct);
            return Result<IdenExt?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<IdenExt?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(i => i.ProvIden)
                .Include(i => i.Estado)
                .Include(i => i.Dispositivo)
                .Include(i => i.UltimoTenantNav)
                .Where(i => i.IdUsuario == idUsuario && !i.Eliminado)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<IdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(i => i.ProvIden)
                .Include(i => i.Estado)
                .Include(i => i.Usuario)
                .Include(i => i.Dispositivo)
                .Include(i => i.UltimoTenantNav)
                .Where(i => i.IdTenant == idTenant && !i.Eliminado)
                .OrderByDescending(i => i.UltimoLogin)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<IdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IdenExt>>> ObtenerPorEstadoAsync(byte idEstado, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(i => i.ProvIden)
                .Include(i => i.Estado)
                .Include(i => i.Usuario)
                .Where(i => i.IdEstado == idEstado && !i.Eliminado)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<IdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IPagedResult<IdenExt>>> BuscarAsync(BuscarIdenExtRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.PageNumber <= 0) request.PageNumber = 1;
            if (request.PageSize <= 0) request.PageSize = 20;

            var query = DbSet
                .Include(i => i.ProvIden)
                .Include(i => i.Estado)
                .Include(i => i.Usuario)
                .Include(i => i.Dispositivo)
                .Include(i => i.UltimoTenantNav)
                .Where(i => !i.Eliminado)
                .AsNoTracking();

            if (request.IdEstado.HasValue)
                query = query.Where(i => i.IdEstado == request.IdEstado.Value);

            if (request.IdProveedor.HasValue)
                query = query.Where(i => i.IdProvIden == request.IdProveedor.Value);

            if (request.IdTenant.HasValue)
                query = query.Where(i => i.IdTenant == request.IdTenant.Value);

            if (!string.IsNullOrWhiteSpace(request.TextoLibre))
            {
                var term = request.TextoLibre.Trim().ToLower();
                query = query.Where(i =>
                    (i.SubExterno != null && i.SubExterno.ToLower().Contains(term)) ||
                    (i.EmailExterno != null && i.EmailExterno.ToLower().Contains(term)) ||
                    (i.ProviderUserName != null && i.ProviderUserName.ToLower().Contains(term)) ||
                    (i.NombreExterno != null && i.NombreExterno.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(request.UsuarioNombre))
            {
                var term = request.UsuarioNombre.Trim().ToLower();
                query = query.Where(i => i.Usuario != null && i.Usuario.NomUsuario.ToLower().Contains(term));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(i => i.UltimoLogin ?? i.FecCrea)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            return Result<IPagedResult<IdenExt>>.Success(new PagedResult<IdenExt>
            {
                Items = items,
                TotalCount = total,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return Result<IPagedResult<IdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<DesgloseProveedorReadModel>>> ObtenerDesglosePorProveedorAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Where(i => i.IdTenant == idTenant && !i.Eliminado)
                .GroupBy(i => new { i.IdProvIden, i.ProvIden!.Codigo, i.ProvIden.Nombre, i.ProvIden.Icono })
                .Select(g => new DesgloseProveedorReadModel
                {
                    IdProvIden = g.Key.IdProvIden,
                    Codigo = g.Key.Codigo,
                    Nombre = g.Key.Nombre,
                    Icono = g.Key.Icono,
                    TotalVinculadas = g.Count()
                })
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<DesgloseProveedorReadModel>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DesgloseProveedorReadModel>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
