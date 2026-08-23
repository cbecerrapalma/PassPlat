using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public class SesionRepository : RepositoryAsync<Sesion>, ISesionRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;
    private readonly ILogger<SesionRepository> _logger;

    public SesionRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow, ILogger<SesionRepository> logger)
        : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
        _logger = logger;
    }

    public async Task<Result<CrearSesionResult>> CrearSesionAsync(int idUsuario, int idTenant, int idApp, string idTokenExt, DateTime fecExpira, string? hashRefresh = null, int? idDisp = null, int? idIP = null, Guid? idSesionPadre = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.NVarChar("@IdTokenExt", idTokenExt, 128),
            RawParameter.NVarChar("@HashRefresh", hashRefresh, 128),
            RawParameter.Date("@FecExpira", fecExpira),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdIP", idIP),
            RawParameter.In("@IdSesionPadre", idSesionPadre, System.Data.DbType.Guid)
        };

        return await SpHelper.ExecuteSPAsync<CrearSesionResult>(_rawQuery, "SP_Sesiones_Crear", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<RevocarSesionesResult>> RevocarTodasAsync(int idUsuario, int idTenant, Guid? idSesionExcluir = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.In("@IdSesionExcluir", idSesionExcluir, System.Data.DbType.Guid)
        };

        return await SpHelper.ExecuteSPAsync<RevocarSesionesResult>(_rawQuery, "SP_Sesiones_RevocarTodas", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default)
    {
        try
        {
            var sesion = await DbSet.FirstOrDefaultAsync(s => s.Id == idSesion && s.EsActiva, ct);
            if (sesion == null)
            {
                _logger.LogWarning("RevocarSesionAsync: sesión {IdSesion} no encontrada o ya inactiva", idSesion);
                return Result.Success();
            }

            sesion.EsActiva = false;
            sesion.HashRefresh = null;
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al revocar sesión {IdSesion}", idSesion);
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<Sesion>>> ObtenerSesionesActivasPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await Query()
                .Include(s => s.Usuario)
                .Include(s => s.App)
                .Include(s => s.Disp)
                .Include(s => s.DireccionIP)
                .Where(s => s.IdUsuario == idUsuario && s.IdTenant == idTenant && s.EsActiva && s.FecExpira > DateTime.Now)
                .OrderByDescending(s => s.FecInicio)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Sesion>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Sesion>>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default)
    {
        try
        {
            var count = await Query()
                .CountAsync(s => s.IdUsuario == idUsuario && s.IdTenant == idTenant && s.EsActiva && s.FecExpira > DateTime.Now, ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> ContarSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var count = await Query()
                .CountAsync(s => s.IdTenant == idTenant && s.EsActiva && s.FecExpira > DateTime.Now, ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Sesion?>> ObtenerPorIdTokenExtAsync(string idTokenExt, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(s => s.IdTokenExt == idTokenExt, ct);
            return Result<Sesion?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Sesion?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Sesion?>> ObtenerSesionActivaPorJtiAsync(int idUsuario, string jti, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(
                s => s.IdUsuario == idUsuario && s.IdTokenExt == jti && s.EsActiva && s.FecExpira > DateTime.Now, ct);
            return Result<Sesion?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Sesion?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<Sesion?>> ObtenerPorHashRefreshAsync(string hashRefresh, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(s => s.HashRefresh == hashRefresh && s.EsActiva, ct);
            return Result<Sesion?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<Sesion?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<int>> DesactivarExpiradasAsync(CancellationToken ct = default)
    {
        try
        {
            var count = await DbSet
                .Where(s => s.EsActiva && s.FecExpira <= DateTime.Now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.EsActiva, false)
                    .SetProperty(s => s.HashRefresh, (string?)null),
                    ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<bool>> IntentarRotarHashRefreshAsync(Guid idSesion, string? hashRefreshEsperado, string? nuevoHashRefresh, DateTime nuevaFecExpira, CancellationToken ct = default)
    {
        try
        {
            var count = await DbSet
                .Where(s => s.Id == idSesion && s.HashRefresh == hashRefreshEsperado)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.HashRefresh, nuevoHashRefresh)
                    .SetProperty(s => s.FecExpira, nuevaFecExpira),
                    ct);
            return Result<bool>.Success(count > 0);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("DB_ERROR", ex.Message);
        }
    }

    // ETAPA 5: sesiones activas de todo el tenant (administración)
    public async Task<Result<IReadOnlyList<Sesion>>> ObtenerSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var list = await Query()
                .Include(s => s.Usuario)
                .Include(s => s.App)
                .Include(s => s.Disp)
                .Include(s => s.DireccionIP)
                .Where(s => s.IdTenant == idTenant && s.EsActiva && s.FecExpira > DateTime.Now)
                .OrderByDescending(s => s.FecInicio)
                .ToListAsync(ct);
            return Result<IReadOnlyList<Sesion>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Sesion>>.Failure("DB_ERROR", ex.Message);
        }
    }

    // ETAPA 5: revocar todas las sesiones activas del tenant (administración)
    public async Task<Result<int>> RevocarTodasPorTenantAsync(int idTenant, CancellationToken ct = default)
    {
        try
        {
            var count = await DbSet
                .Where(s => s.IdTenant == idTenant && s.EsActiva)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.EsActiva, false)
                    .SetProperty(s => s.HashRefresh, (string?)null)
                    .SetProperty(s => s.UltActividad, DateTime.Now),
                    ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("DB_ERROR", ex.Message);
        }
    }
}