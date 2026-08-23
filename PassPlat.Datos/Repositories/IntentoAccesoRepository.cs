using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IIntentoAccesoRepository : IRepositoryAsync<IntentoAcceso>
{
    Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosPorIPAsync(int idIP, int minutos, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIPsSospechosasAsync(int minutos, int umbral, CancellationToken ct = default);
    Result<IntentoAcceso> RegistrarIntento(int idResultado, string nomUsuarioIntentado, bool exitoso, int? idUsuario = null, int? idTenant = null, int? idApp = null, int? idDisp = null, int? idAgente = null, int? idIP = null, string? detResultado = null, int? tpoRespuesta = null, int? codRespuesta = null, string? metodoAutenticacion = null);
    Task<Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>> GetPagedWithIncludesAsync(int pageNumber, int pageSize, int? idTenant = null, bool includeTotal = true, CancellationToken ct = default);
}

public class IntentoAccesoRepository : RepositoryAsync<IntentoAcceso>, IIntentoAccesoRepository
{
    public IntentoAccesoRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>> GetPagedWithIncludesAsync(
        int pageNumber, int pageSize, int? idTenant = null, bool includeTotal = true, CancellationToken ct = default)
    {
        try
        {
            var query = DbSet.Include(i => i.Resultado).Include(i => i.DireccionIP)
                .AsNoTracking()
                .AsQueryable();
            if (idTenant.HasValue)
                query = query.Where(i => i.IdTenant == idTenant.Value);
            query = query.OrderByDescending(i => i.FecIntento);
            var total = includeTotal ? await query.CountAsync(ct) : 0;
            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>.Success((items, total));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<IntentoAcceso> Items, int TotalCount)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            var result = await DbSet.Include(i => i.Resultado).Include(i => i.DireccionIP)
                .Where(i => i.IdUsuario == idUsuario && i.FecIntento >= desde)
                .OrderByDescending(i => i.FecIntento).AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<IntentoAcceso>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IntentoAcceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            return Result<int>.Success(await DbSet.CountAsync(i => i.IdUsuario == idUsuario && !i.Exitoso && i.FecIntento >= desde, ct));
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarIntentosFallidosPorIPAsync(int idIP, int minutos, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            return Result<int>.Success(await DbSet.CountAsync(i => i.IdIP == idIP && !i.Exitoso && i.FecIntento >= desde, ct));
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IntentoAcceso>>> ObtenerIPsSospechosasAsync(int minutos, int umbral, CancellationToken ct = default)
    {
        try
        {
            var desde = DateTime.Now.AddMinutes(-minutos);
            var ipsSospechosas = await Query().Where(i => !i.Exitoso && i.FecIntento >= desde)
                .GroupBy(i => i.IdIP)
                .Where(g => g.Count() >= umbral)
                .Select(g => g.Key)
                .ToListAsync(ct);

            if (ipsSospechosas.Count == 0) return Result<IReadOnlyList<IntentoAcceso>>.Success([]);

            var result = await Query().Where(i => ipsSospechosas.Contains(i.IdIP) && i.FecIntento >= desde)
                .OrderByDescending(i => i.FecIntento)
                .AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<IntentoAcceso>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IntentoAcceso>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<IntentoAcceso> RegistrarIntento(int idResultado, string nomUsuarioIntentado, bool exitoso, int? idUsuario = null, int? idTenant = null, int? idApp = null, int? idDisp = null, int? idAgente = null, int? idIP = null, string? detResultado = null, int? tpoRespuesta = null, int? codRespuesta = null, string? metodoAutenticacion = null)
    {
        try
        {
            var intento = new IntentoAcceso
            {
                IdUsuario = idUsuario, IdTenant = idTenant, IdApp = idApp,
                IdResultado = idResultado, NomUsuarioIntentado = nomUsuarioIntentado,
                Exitoso = exitoso, IdDisp = idDisp, IdAgente = idAgente, IdIP = idIP,
                DetResultado = detResultado, TpoRespuesta = tpoRespuesta,
                CodRespuesta = codRespuesta, FecIntento = DateTime.Now,
                MetodoAutenticacion = metodoAutenticacion ?? "Local"
            };
            DbSet.Add(intento);
            return Result<IntentoAcceso>.Success(intento);
        }
        catch (Exception ex)
        {
            return Result<IntentoAcceso>.Failure("DB_ERROR", ex.Message);
        }
    }
}
