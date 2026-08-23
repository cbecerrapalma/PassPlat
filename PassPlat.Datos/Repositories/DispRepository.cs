using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Contexto;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IDispRepository : IRepositoryAsync<Disp>
{
    Task<Result<Disp?>> ObtenerPorIdAsync(int idDisp, CancellationToken ct = default);
    Result<Disp> ObtenerOCrear(int idTipoDisp, string? fabricante = null, string? modelo = null);
    Task<Result<IReadOnlyList<Disp>>> ObtenerTodosConTipoAsync(CancellationToken ct = default);
    Task<Result<Disp?>> ObtenerConDetallesAsync(int idDisp, CancellationToken ct = default);
    Result IncrementarLogin(int idDisp);
}

public class DispRepository : RepositoryAsync<Disp>, IDispRepository
{
    public DispRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<Disp?>> ObtenerPorIdAsync(int idDisp, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(d => d.Id == idDisp, ct);
            return Result<Disp?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Disp?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<Disp> ObtenerOCrear(int idTipoDisp, string? fabricante = null, string? modelo = null)
    {
        try
        {
            var disp = DbSet.FirstOrDefault(d => d.IdTipoDisp == idTipoDisp && d.Fabricante == fabricante && d.Modelo == modelo);
            if (disp != null)
            {
                disp.UltActividad = DateTime.Now;
                return Result<Disp>.Success(disp);
            }
            disp = new Disp { IdTipoDisp = idTipoDisp, Fabricante = fabricante, Modelo = modelo, FecPrimerReg = DateTime.Now };
            DbSet.Add(disp);
            return Result<Disp>.Success(disp);
        }
        catch (Exception ex)
        {
            return Result<Disp>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Disp>>> ObtenerTodosConTipoAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Include(d => d.TipoDisp).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<Disp>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Disp>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Disp?>> ObtenerConDetallesAsync(int idDisp, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.Include(d => d.TipoDisp).FirstOrDefaultAsync(d => d.Id == idDisp, ct);
            return Result<Disp?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Disp?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result IncrementarLogin(int idDisp)
    {
        try
        {
            var disp = DbSet.Find(idDisp);
            if (disp != null)
            {
                disp.CantidadLogins++;
                disp.UltActividad = DateTime.Now;
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }
}
