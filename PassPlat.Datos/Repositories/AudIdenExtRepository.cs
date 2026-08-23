using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IAudIdenExtRepository : IRepositoryAsync<AudIdenExt>
{
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorProveedorAsync(int idProvIden, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorMetodoAsync(string metodo, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTipoLoginAsync(string tipoLogin, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorOrigenAsync(string origen, int limite = 50, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorRangoFechasAsync(int idTenant, DateTime desde, DateTime hasta, int limite = 100, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerErroresAsync(int idTenant, int limite = 50, CancellationToken ct = default);
}

public class AudIdenExtRepository : RepositoryAsync<AudIdenExt>, IAudIdenExtRepository
{
    public AudIdenExtRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTenantAsync(int idTenant, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdTenant == idTenant)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorProveedorAsync(int idProvIden, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdProvIden == idProvIden)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorUsuarioAsync(int idUsuario, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdUsuario == idUsuario)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorMetodoAsync(string metodo, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.MetodoAutenticacion == metodo)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorTipoLoginAsync(string tipoLogin, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.TipoLogin == tipoLogin)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorOrigenAsync(string origen, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.Origen == origen)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerPorRangoFechasAsync(int idTenant, DateTime desde, DateTime hasta, int limite = 100, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdTenant == idTenant && a.FecEvento >= desde && a.FecEvento <= hasta)
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AudIdenExt>>> ObtenerErroresAsync(int idTenant, int limite = 50, CancellationToken ct = default)
    {
        try
        {
            var list = await DbSet
                .Include(a => a.ProvIden)
                .Where(a => a.IdTenant == idTenant && (a.Resultado == "Error" || a.HttpStatus >= 400 || a.Codigo != null))
                .OrderByDescending(a => a.FecEvento)
                .Take(limite)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<AudIdenExt>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AudIdenExt>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
