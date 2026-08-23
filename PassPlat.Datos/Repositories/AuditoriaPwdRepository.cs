using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IAuditoriaPwdRepository : IRepositoryAsync<AuditoriaPwd>
{
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorUsuarioAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorTipoAccionAsync(int idTipoAccion, DateTime? desde = null, int? cantidad = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerEventosAltoRiesgoAsync(int nivelMinimo, int cantidad, CancellationToken ct = default);
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
        catch (Exception ex)
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
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AuditoriaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerPorTipoAccionAsync(int idTipoAccion, DateTime? desde = null, int? cantidad = null, CancellationToken ct = default)
    {
        try
        {
            var query = Query().Where(a => a.IdTipoAccion == idTipoAccion);
            if (desde.HasValue) query = query.Where(a => a.FecAccion >= desde.Value);
            query = query.OrderByDescending(a => a.FecAccion);
            if (cantidad.HasValue) query = query.Take(cantidad.Value);
            var result = await query.ToListAsync(ct);
            return Result<IReadOnlyList<AuditoriaPwd>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AuditoriaPwd>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<AuditoriaPwd>>> ObtenerEventosAltoRiesgoAsync(int nivelMinimo, int cantidad, CancellationToken ct = default)
    {
        try
        {
            var result = await Query().Where(a => a.NivelRiesgo >= nivelMinimo).OrderByDescending(a => a.FecAccion).Take(cantidad).ToListAsync(ct);
            return Result<IReadOnlyList<AuditoriaPwd>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AuditoriaPwd>>.Failure("DB_ERROR", ex.Message);
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
        catch (Exception ex)
        {
            return Result<AuditoriaPwd>.Failure("DB_ERROR", ex.Message);
        }
    }
}
