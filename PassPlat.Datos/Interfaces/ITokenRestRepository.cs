using CBP.Data.Abstractions;
using CBP.Results;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Interfaces;

public interface ITokenRestRepository : IRepositoryAsync<TokenRest>
{
    Task<Result<GenerarTokenResult>> GenerarTokenAsync(int idUsuario, int idTenant, int idApp, string hashToken, DateTime fecVence, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default);
    Task<Result<ValidarTokenResult>> ValidarTokenAsync(string hashToken, int? idApp = null, CancellationToken ct = default);
}