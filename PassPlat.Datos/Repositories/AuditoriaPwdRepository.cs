using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;
using PassPlat.Datos.Models;

namespace PassPlat.Datos.Repositories;

public interface IAuditoriaPwdRepository : IRepositoryAsync<AuditoriaPwd>
{
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorUsuarioAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<AuditoriaPwd> Items, int TotalCount)>> BuscarAsync(FiltroAuditoriaPwd filtro, int pageNumber, int pageSize, CancellationToken ct = default);
    Result<AuditoriaPwd> RegistrarAuditoria(int idUsuario, int idTipoAccion, int? idTenant = null, int? idApp = null, int? idUsrEjecutor = null, int? idDisp = null, int? idAgente = null, int? idIP = null, long? idHistPwd = null, string? detalles = null, int? nivelRiesgo = null, string? metadata = null);
}

public class AuditoriaPwdRepository : RepositoryAsync<AuditoriaPwd>, IAuditoriaPwdRepository
{
    public AuditoriaPwdRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorUsuarioAsync(int idUsuario, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await DbSet.Include(a => a.TipoAccion).Where(a => a.IdUsuario == idUsuario).OrderByDescending(a => a.FecAccion).Take(cantidad).ToListAsync(ct);
            return Result<IReadOnlyList<AuditoriaPwd>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<AuditoriaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Where(a => a.IdTenant == idTenant).OrderByDescending(a => a.FecAccion).Take(cantidad).ToListAsync(ct);
            return Result<IReadOnlyList<AuditoriaPwd>>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<AuditoriaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // S95 · D1 — filtros y paginación en la consulta (antes: las primeras N filas y Skip en memoria)
    public async Task<Result<(IReadOnlyList<AuditoriaPwd> Items, int TotalCount)>> BuscarAsync(FiltroAuditoriaPwd filtro, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        try
        {
            IQueryable<AuditoriaPwd> query = DbSet.AsNoTracking()
                .Include(a => a.TipoAccion)
                .Include(a => a.Usuario);

            if (filtro.IdTenant.HasValue)
                query = query.Where(a => a.IdTenant == filtro.IdTenant.Value);
            if (filtro.IdUsuario.HasValue)
                query = query.Where(a => a.IdUsuario == filtro.IdUsuario.Value);
            if (filtro.IdTipoAccion.HasValue)
                query = query.Where(a => a.IdTipoAccion == filtro.IdTipoAccion.Value);
            if (filtro.NivelMinimo.HasValue)
                query = query.Where(a => a.NivelRiesgo >= filtro.NivelMinimo.Value);
            if (filtro.Desde.HasValue)
            {
                var desde = filtro.Desde.Value.Date;
                query = query.Where(a => a.FecAccion >= desde);
            }
            if (filtro.Hasta.HasValue)
            {
                var antesDe = filtro.Hasta.Value.Date.AddDays(1);
                query = query.Where(a => a.FecAccion < antesDe);
            }
            if (!string.IsNullOrWhiteSpace(filtro.Texto))
            {
                var texto = filtro.Texto.Trim();
                query = query.Where(a =>
                    (a.Detalles != null && a.Detalles.Contains(texto)) ||
                    (a.Usuario != null && a.Usuario.NomUsuario.Contains(texto)));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(a => a.FecAccion)
                .ThenByDescending(a => a.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<(IReadOnlyList<AuditoriaPwd>, int)>.Success((items, total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(IReadOnlyList<AuditoriaPwd>, int)>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<AuditoriaPwd> RegistrarAuditoria(int idUsuario, int idTipoAccion, int? idTenant = null, int? idApp = null, int? idUsrEjecutor = null, int? idDisp = null, int? idAgente = null, int? idIP = null, long? idHistPwd = null, string? detalles = null, int? nivelRiesgo = null, string? metadata = null)
    {
        try
        {
            var auditoria = AuditoriaPwd.Crear(idUsuario, idTipoAccion, idUsrEjecutor);
            auditoria.IdTenant = idTenant; auditoria.IdApp = idApp;
            auditoria.IdDisp = idDisp; auditoria.IdAgente = idAgente; auditoria.IdIP = idIP;
            auditoria.IdHistPwd = idHistPwd; auditoria.Detalles = detalles;
            auditoria.NivelRiesgo = nivelRiesgo; auditoria.Metadata = metadata;
            DbSet.Add(auditoria);
            return Result<AuditoriaPwd>.Success(auditoria);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<AuditoriaPwd>.Failure("DB_ERROR", ex.Message);
        }
    }
}
