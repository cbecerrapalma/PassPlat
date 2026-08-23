using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public interface IIdenExtTokensRepository : IRepositoryAsync<IdenExtTokens>
{
    Task<Result<IdenExtTokens?>> ObtenerTokenActivoAsync(long idIdenExt, CancellationToken ct = default);
    Task<Result> RevocarTokensAsync(long idIdenExt, string? motivo = null, CancellationToken ct = default);
    Task<Result<long?>> ExisteRefreshTokenHashAsync(string hash, CancellationToken ct = default);
    Task<Result> ActualizarUltimoUsoAsync(long id, CancellationToken ct = default);
    Task<Result> MarcarTokenAnteriorInactivoAsync(long idIdenExt, int versionActual, CancellationToken ct = default);
    Task<Result<IReadOnlyList<IdenExtTokens>>> ObtenerTokensPorRenovarAsync(TimeSpan threshold, CancellationToken ct = default);
}

public class IdenExtTokensRepository : RepositoryAsync<IdenExtTokens>, IIdenExtTokensRepository
{
    public IdenExtTokensRepository(PassPlatDbContext context) : base(context) { }

    public async Task<Result<IdenExtTokens?>> ObtenerTokenActivoAsync(long idIdenExt, CancellationToken ct = default)
    {
        try
        {
            var token = await DbSet
                .Where(t => t.IdIdenExt == idIdenExt && t.Activo && !t.Revocado)
                .OrderByDescending(t => t.Version)
                .FirstOrDefaultAsync(ct);
            return Result<IdenExtTokens?>.Success(token, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<IdenExtTokens?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> RevocarTokensAsync(long idIdenExt, string? motivo = null, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.Now;
            foreach (var t in await DbSet.Where(t => t.IdIdenExt == idIdenExt && t.Activo && !t.Revocado).ToListAsync(ct))
            {
                t.Activo = false;
                t.Revocado = true;
                t.FechaRevocacion = now;
                t.MotivoRevocacion = motivo;
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<long?>> ExisteRefreshTokenHashAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            var id = await DbSet
                .Where(t => t.RefreshTokenHash == hash && t.Activo && !t.Revocado)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync(ct);
            return Result<long?>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<long?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> ActualizarUltimoUsoAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], ct);
            if (entity == null)
                return Result.Failure("NOT_FOUND", "Token no encontrado");
            entity.UltimoUso = DateTime.Now;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result> MarcarTokenAnteriorInactivoAsync(long idIdenExt, int versionActual, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.Now;
            foreach (var t in await DbSet
                .Where(t => t.IdIdenExt == idIdenExt && t.Activo && t.Version < versionActual)
                .ToListAsync(ct))
            {
                t.Activo = false;
                t.Revocado = true;
                t.FechaRevocacion = now;
                t.MotivoRevocacion = "Reemplazado por nueva versión";
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("DB_ERROR", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<IdenExtTokens>>> ObtenerTokensPorRenovarAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        try
        {
            var cutoff = DateTime.Now.Add(threshold);
            var list = await DbSet
                .Include(t => t.IdenExt)
                .ThenInclude(i => i!.ProvIden)
                .Where(t => t.Activo && !t.Revocado && t.RefreshTokenEnc != null && t.RefreshTokenExpires != null && t.RefreshTokenExpires < cutoff && t.RefreshTokenExpires > DateTime.Now)
                .AsNoTracking()
                .ToListAsync(ct);
            return Result<IReadOnlyList<IdenExtTokens>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<IdenExtTokens>>.Failure("DB_ERROR", ex.Message);
        }
    }
}
