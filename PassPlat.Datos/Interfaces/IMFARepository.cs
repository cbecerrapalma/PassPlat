using CBP.Data.Abstractions;
using CBP.Results;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Interfaces;

public interface IMFARepository : IRepositoryAsync<MFA>
{
    Task<Result<ValidarMFAResult>> ValidarMFAAsync(int idUsuario, int idTenant, int idTipoMFA, string idMFA, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MFA>>> ObtenerMetodosPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<MFA?>> ObtenerMetodoPrincipalAsync(int idUsuario, CancellationToken ct = default);
    Result RevocarMetodo(int idUsuario, int idMFARegistro);
}