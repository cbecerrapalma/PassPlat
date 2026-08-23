using CBP.Data.Abstractions;
using CBP.Results;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Interfaces;

public interface IPasswordRepository : IRepositoryAsync<HistorialPwd>
{
    Task<Result<CambiarPwdResult>> CambiarPasswordAsync(int idUsuario, int idTenant, string hashPwdNuevo, byte pepperVersion, int idTipoCambio, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default);
}