using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IHistorialPwdRepository : IRepositoryAsync<HistorialPwd>
{
    Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result<bool>> PasswordRepetidaAsync(int idUsuario, string hashPwd, int historialCant, CancellationToken ct = default);
    Task<Result> MarcarComprometidasPorHashAsync(string hashPwd, CancellationToken ct = default);
    Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerPasswordsComprometidasAsync(CancellationToken ct = default);
    Task<Result<(IReadOnlyList<HistorialPwd> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default);
    Result DesactivarPasswordActual(int idUsuario);
}

public class HistorialPwdRepository : RepositoryAsync<HistorialPwd>, IHistorialPwdRepository
{
    public HistorialPwdRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerHistorialRecienteAsync(int idUsuario, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Include(h => h.Politica).Where(h => h.IdUsuario == idUsuario).OrderByDescending(h => h.FecRegistro).Take(cantidad).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<HistorialPwd>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HistorialPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> PasswordRepetidaAsync(int idUsuario, string hashPwd, int historialCant, CancellationToken ct = default)
    {
        try
        {
            return Result<bool>.Success(await DbSet.Where(h => h.IdUsuario == idUsuario).OrderByDescending(h => h.FecRegistro)
                .Take(historialCant).AnyAsync(h => h.HashPwd == hashPwd, ct));
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarComprometidasPorHashAsync(string hashPwd, CancellationToken ct = default)
    {
        try
        {
            await DbSet.Where(h => h.HashPwd == hashPwd && !h.EsComprometida)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(h => h.EsComprometida, true), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<HistorialPwd>>> ObtenerPasswordsComprometidasAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Where(h => h.EsComprometida && h.EsActual).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<HistorialPwd>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<HistorialPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<(IReadOnlyList<HistorialPwd> Items, int TotalCount)>> ObtenerPaginadoPorTenantAsync(int idTenant, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(h => h.Usuario).Where(h => h.Usuario != null && h.Usuario.IdTenant == idTenant).AsNoTracking();
            var totalCount = await query.CountAsync(ct);
            var items = await query.OrderByDescending(h => h.FecRegistro)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Result<(IReadOnlyList<HistorialPwd>, int)>.Success((items, totalCount));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<HistorialPwd>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result DesactivarPasswordActual(int idUsuario)
    {
        try
        {
            var passwordActual = DbSet.FirstOrDefault(h => h.IdUsuario == idUsuario && h.EsActual);
            if (passwordActual == null)
                return Result.Failure("PWD_NOT_FOUND", "No se encontró password actual para el usuario");

            passwordActual.EsActual = false;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}