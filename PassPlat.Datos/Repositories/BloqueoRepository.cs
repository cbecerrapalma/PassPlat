using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IBloqueoRepository : IRepositoryAsync<Bloqueo>
{
    Task<Result<bool>> EstaBloqueadoAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<Bloqueo?>> ObtenerBloqueoActivoAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Result<Bloqueo> CrearBloqueo(int idUsuario, int idTenant, int idTipoBloqueo, string motivo, DateTime? fecFin = null, int? idAgente = null, int? idIP = null, int? idUsrBloqueador = null, string? tipoDeteccion = null);
    Task<Result<IReadOnlyList<Bloqueo>>> ObtenerBloqueosTemporalesVencidosAsync(CancellationToken ct = default);
    Task<Result> DesactivarBloqueosVencidosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Bloqueo>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default);
}

public class BloqueoRepository : RepositoryAsync<Bloqueo>, IBloqueoRepository
{
    public BloqueoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<bool>> EstaBloqueadoAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            return Result<bool>.Success(await DbSet.Include(b => b.TipoBloqueo)
                .AnyAsync(b => b.IdUsuario == idUsuario && b.IdTenant == idTenant && b.Activo
                    && (b.TipoBloqueo == null || !b.TipoBloqueo.EsTemporal || !b.FecFin.HasValue || b.FecFin.Value > DateTime.Now), ct));
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Bloqueo?>> ObtenerBloqueoActivoAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var bloqueo = await DbSet.Include(b => b.TipoBloqueo)
                .FirstOrDefaultAsync(b => b.IdUsuario == idUsuario && b.IdTenant == idTenant && b.Activo
                    && (b.TipoBloqueo == null || !b.TipoBloqueo.EsTemporal || !b.FecFin.HasValue || b.FecFin.Value > DateTime.Now), ct);
            return Result<Bloqueo?>.Success(bloqueo, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Bloqueo?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<Bloqueo> CrearBloqueo(int idUsuario, int idTenant, int idTipoBloqueo, string motivo, DateTime? fecFin = null, int? idAgente = null, int? idIP = null, int? idUsrBloqueador = null, string? tipoDeteccion = null)
    {
        try
        {
            var bloqueo = Bloqueo.Crear(idUsuario, idTenant, idTipoBloqueo, motivo, fecFin);
            bloqueo.IdAgente = idAgente;
            bloqueo.IdIP = idIP;
            bloqueo.IdUsrBloqueador = idUsrBloqueador;
            bloqueo.TipoDeteccion = tipoDeteccion;
            DbSet.Add(bloqueo);
            return Result<Bloqueo>.Success(bloqueo);
        }
        catch (Exception ex)
        {
            return Result<Bloqueo>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Bloqueo>>> ObtenerBloqueosTemporalesVencidosAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Include(b => b.TipoBloqueo)
                .Where(b => b.Activo && b.TipoBloqueo != null && b.TipoBloqueo.EsTemporal && b.FecFin.HasValue && b.FecFin.Value <= DateTime.Now)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Bloqueo>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Bloqueo>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> DesactivarBloqueosVencidosAsync(CancellationToken ct = default)
    {
        try
        {
            await DbSet.Where(b => b.Activo && b.TipoBloqueo != null && b.TipoBloqueo.EsTemporal && b.FecFin.HasValue && b.FecFin.Value <= DateTime.Now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.Activo, false)
                    .SetProperty(b => b.FecFin, b => b.FecFin ?? DateTime.Now), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Bloqueo>>> ObtenerBloqueosPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Include(b => b.TipoBloqueo)
                .Where(b => b.IdUsuario == idUsuario && b.IdTenant == idTenant)
                .OrderByDescending(b => b.FecInicio)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Bloqueo>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Bloqueo>>.Failure("DB_ERROR", ex.Message);
        }
    }
}