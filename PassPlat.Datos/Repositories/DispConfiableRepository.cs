using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IDispConfiableRepository : IRepositoryAsync<DispConfiable>
{
    Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default);
    Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, int? idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(int? idTenant, CancellationToken ct = default);
    Task<Result<DispConfiable?>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Result RevocarConfianza(int idUsuario, int idDisp);
    Result RevocarConfianza(int idUsuario, int idDisp, int? idTenant);
    // S80 · D1 — devuelve la entidad (existente reactivada o nueva agregada; misma instancia del tracker)
    Result<DispConfiable> MarcarComoConfiable(int idUsuario, int idTenant, int idDisp, string? nombre = null, int? idAgente = null);
}

public class DispConfiableRepository : RepositoryAsync<DispConfiable>, IDispConfiableRepository
{
    public DispConfiableRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, CancellationToken ct = default)
        => EsConfiableAsync(idUsuario, idDisp, null, ct);

    // S79 · D2 — idTenant null = modo Plataforma (sin filtro de tenant)
    public async Task<Result<bool>> EsConfiableAsync(int idUsuario, int idDisp, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            return Result<bool>.Success(await DbSet.AnyAsync(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.Confiable
                && (idTenant == null || d.IdTenant == idTenant), ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
        => ObtenerPorUsuarioAsync(idUsuario, null, ct);

    // S75 · D2 — idTenant null = modo Plataforma (sin filtro de tenant)
    public async Task<Result<IReadOnlyList<DispConfiable>>> ObtenerPorUsuarioAsync(int idUsuario, int? idTenant, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Where(d => d.IdUsuario == idUsuario)
                .Where(d => idTenant == null || d.IdTenant == idTenant)
                .Include(d => d.Disp).ThenInclude(d => d!.TipoDisp)
                .Include(d => d.Usuario)
                .AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<DispConfiable>>.Success(result);
        }
        // S96.1 · D4 — una cancelación del request no es DB_ERROR: la ignora UseCbpExceptionHandler
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<DispConfiable>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(CancellationToken ct = default)
        => ObtenerTodosConDispositivoAsync(null, ct);

    // S75 · D2 — idTenant null = modo Plataforma (sin filtro de tenant)
    public async Task<Result<IReadOnlyList<DispConfiable>>> ObtenerTodosConDispositivoAsync(int? idTenant, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Where(d => idTenant == null || d.IdTenant == idTenant)
                .Include(d => d.Disp).ThenInclude(d => d!.TipoDisp)
                .Include(d => d.Usuario)
                .AsNoTracking().ToListAsync(ct);
            return Result<IReadOnlyList<DispConfiable>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<DispConfiable>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result RevocarConfianza(int idUsuario, int idDisp)
        => RevocarConfianza(idUsuario, idDisp, null);

    // S79 · D2 — revoca todas las filas confiables del par en el tenant; null = todos los tenants
    public Result RevocarConfianza(int idUsuario, int idDisp, int? idTenant)
    {
        try
        {
            var dispositivos = DbSet.Where(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.Confiable
                && (idTenant == null || d.IdTenant == idTenant)).ToList();
            if (dispositivos.Count == 0)
                return Result.Failure("DISP_NOT_FOUND", "Dispositivo confiable no encontrado");

            foreach (var dispositivo in dispositivos)
                dispositivo.Confiable = false;
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<DispConfiable?>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S80 · D1 — devuelve la fila existente reactivada o la nueva agregada (misma instancia del ChangeTracker)
    public Result<DispConfiable> MarcarComoConfiable(int idUsuario, int idTenant, int idDisp, string? nombre = null, int? idAgente = null)
    {
        try
        {
            // S79 · D2 — la fila existente se busca solo en el tenant indicado
            var existente = DbSet.FirstOrDefault(d => d.IdUsuario == idUsuario && d.IdDisp == idDisp && d.IdTenant == idTenant);
            if (existente != null)
            {
                existente.Confiable = true;
                existente.UltUso = DateTime.Now;
                return Result<DispConfiable>.Success(existente);
            }
            var nueva = new DispConfiable
            {
                IdUsuario = idUsuario, IdTenant = idTenant, IdDisp = idDisp,
                Nombre = nombre, Confiable = true, FecAlta = DateTime.Now,
                UltUso = DateTime.Now, IdAgente = idAgente
            };
            DbSet.Add(nueva);
            return Result<DispConfiable>.Success(nueva);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<DispConfiable>.Failure("DB_ERROR", ex.Message);
        }
    }
}
