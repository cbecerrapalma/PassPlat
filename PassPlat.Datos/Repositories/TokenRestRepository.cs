using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Repositories;

public class TokenRestRepository : RepositoryAsync<TokenRest>, ITokenRestRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public TokenRestRepository(PassPlatDbContext dbContext, IUnitOfWorkAsync uow)
        : base(dbContext)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<GenerarTokenResult>> GenerarTokenAsync(int idUsuario, int idTenant, int idApp, string hashToken, DateTime fecVence, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.NVarChar("@HashToken", hashToken, 255),
            RawParameter.Date("@FecVence", fecVence),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdIP", idIP),
            RawParameter.Int("@IdAgente", idAgente)
        };

        return await SpHelper.ExecuteSPAsync<GenerarTokenResult>(_rawQuery, "SP_TokensRest_Generar", parameters, "Sin resultado del SP", ct);
    }

    public async Task<Result<ValidarTokenResult>> ValidarTokenAsync(string hashToken, int? idApp = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.NVarChar("@HashToken", hashToken, 255),
            RawParameter.Int("@IdApp", idApp)
        };

        return await SpHelper.ExecuteSPAsync<ValidarTokenResult>(_rawQuery, "SP_TokensRest_Validar", parameters, "Sin resultado del SP", ct);
    }
}