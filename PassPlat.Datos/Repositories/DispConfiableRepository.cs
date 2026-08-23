using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IDispConfiableRepository : IRepositoryAsync<DispConfiable>
{
    Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default);
    Task<Result<DispConfiable?>> ObtenerPorUsuarioYDispositivoAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerDispositivosInactivosAsync(int diasInactividad, CancellationToken ct = default);
    Task<Result<DispConfiable?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Result RevocarConfianza(int idUsuario, int idDisp);
    Result MarcarComoConfiable(int idUsuario, int idTenant, int idDisp, string? nombre = null, int? idAgente = null);
}

public class DispConfiableRepository : RepositoryAsync<DispConfiable>, IDispConfiableRepository
{
    public DispConfiableRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default)
    {
        try
        {
            return Result<bool>.Success(await DbSet.AnyAsync(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.Confiable, ct));
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Where(d => d.IdUsuario == idUsuario)
                .Include(d => d.Disp).ThenInclude(d => d!.TipoDisp)
                .Include(d => d.Usuario)
                .ToListAsync(ct);
            return Result<IReadOnlyList<DispConfiable>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DispConfiable>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Include(d => d.Disp).ThenInclude(d => d!.TipoDisp)
                .Include(d => d.Usuario)
                .AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<DispConfiable>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DispConfiable>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<DispConfiable?>> ObtenerPorUsuarioYDispositivoAsync(int idUsuario, int idDisp, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.FirstOrDefaultAsync(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp, ct);
            return Result<DispConfiable?>.Success(result, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<DispConfiable?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<DispConfiable>>> ObtenerDispositivosInactivosAsync(int diasInactividad, CancellationToken ct = default)
    {
        try
        {
            var fechaLimite = DateTime.Now.AddDays(-diasInactividad);
            var result = await Query().Where(d => d.UltUso.HasValue && d.UltUso.Value <= fechaLimite).ToListAsync(ct);
            return Result<IReadOnlyList<DispConfiable>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DispConfiable>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result RevocarConfianza(int idUsuario, int idDisp)
    {
        try
        {
            var dispositivo = DbSet.FirstOrDefault(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.Confiable);
            if (dispositivo == null)
                return Result.Failure("DISP_NOT_FOUND", "Dispositivo confiable no encontrado");

            dispositivo.Confiable = false;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<DispConfiable?>> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.FirstOrDefaultAsync(d => d.Id == id, ct);
            return Result<DispConfiable?>.Success(result, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<DispConfiable?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result MarcarComoConfiable(int idUsuario, int idTenant, int idDisp, string? nombre = null, int? idAgente = null)
    {
        try
        {
            var existente = DbSet.FirstOrDefault(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp);
            if (existente != null)
            {
                existente.Confiable = true;
                existente.UltUso = DateTime.Now;
                return Result.Success();
            }
            DbSet.Add(new DispConfiable
            {
                IdUsuario = idUsuario, IdTenant = idTenant, IdDisp = idDisp,
                Nombre = nombre, Confiable = true, FecAlta = DateTime.Now,
                UltUso = DateTime.Now, IdAgente = idAgente
            });
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}
