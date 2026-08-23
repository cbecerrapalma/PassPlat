using CBP.Data.Abstractions;
using CBP.Results;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Interfaces;

public interface IAuthRepository : IRepositoryAsync<Usuario>
{
    Task<Result<LoginResult>> LoginAsync(string? nomUsuario, string? email, int idApp, string hashPwdCalculado, int idTenant, int? idDisp = null, int? idIP = null, int? idAgente = null, CancellationToken ct = default);
    Task<Result<Usuario?>> ObtenerUsuarioBasicoAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<Usuario?>> ObtenerUsuarioPorNomAsync(string? nomUsuario, string? email, CancellationToken ct = default);
    Task<Result<string?>> ObtenerHashActualAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<string?>> ObtenerRolCodigoPorAccesoAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPorUsuarioAsync(int idUsuario, int idTenant, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPorUsuarioTenantAsync(int idUsuarioTenant, int idApp, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> ObtenerCodigosPermisosPlatformAsync(int idUsuario, int idApp, CancellationToken ct = default);
    Task<Result<bool>> ExisteAccesoPlatformActivoAsync(int idUsuario, int idApp, CancellationToken ct = default);
}