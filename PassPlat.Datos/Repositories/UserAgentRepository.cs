using CBP.Data.Abstractions;
using CBP.Results;
using Microsoft.EntityFrameworkCore;
using PassPlat.Dominio.Entities.Contexto;
using CBP.Data.Asynchronous;

namespace PassPlat.Datos.Repositories;

public interface IUserAgentRepository : IRepositoryAsync<UserAgent>
{
    Task<Result<UserAgent?>> ObtenerPorHashAsync(string hashAgente, CancellationToken ct = default);
    Result<UserAgent> ObtenerOCrear(string agente, string hashAgente, string? navegador = null, string? version = null, string? sistemaOperativo = null, bool? esMovil = null);
}

public class UserAgentRepository : RepositoryAsync<UserAgent>, IUserAgentRepository
{
    public UserAgentRepository(PassPlatDbContext dbContext) : base(dbContext) { }

    public async Task<Result<UserAgent?>> ObtenerPorHashAsync(string hashAgente, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet.FirstOrDefaultAsync(u => u.HashAgente == hashAgente, ct);
            return Result<UserAgent?>.Success(entity, allowNull: true);
        }
        catch (Exception ex)
        {
            return Result<UserAgent?>.Failure("DB_ERROR", ex.Message);
        }
    }

    public Result<UserAgent> ObtenerOCrear(string agente, string hashAgente, string? navegador = null, string? version = null, string? sistemaOperativo = null, bool? esMovil = null)
    {
        try
        {
            var existente = DbSet.FirstOrDefault(u => u.HashAgente == hashAgente);
            if (existente != null)
            {
                existente.VecesUsado++;
                existente.FecUltUso = DateTime.Now;
                return Result<UserAgent>.Success(existente);
            }
            var userAgent = new UserAgent
            {
                Agente = agente, HashAgente = hashAgente, Navegador = navegador,
                Version = version, SistemaOperativo = sistemaOperativo, EsMovil = esMovil,
                FecPrimerUso = DateTime.Now, VecesUsado = 1
            };
            DbSet.Add(userAgent);
            return Result<UserAgent>.Success(userAgent);
        }
        catch (Exception ex)
        {
            return Result<UserAgent>.Failure("DB_ERROR", ex.Message);
        }
    }
}
