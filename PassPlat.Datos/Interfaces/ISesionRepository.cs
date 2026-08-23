using CBP.Data.Abstractions;
using CBP.Results;
using PassPlat.Datos.SPResults;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Interfaces;

public interface ISesionRepository : IRepositoryAsync<Sesion>
{
    Task<Result<CrearSesionResult>> CrearSesionAsync(int idUsuario, int idTenant, int idApp, string idTokenExt, DateTime fecExpira, string? hashRefresh = null, int? idDisp = null, int? idIP = null, Guid? idSesionPadre = null, CancellationToken ct = default);
    Task<Result<RevocarSesionesResult>> RevocarTodasAsync(int idUsuario, int idTenant, Guid? idSesionExcluir = null, CancellationToken ct = default);
    Task<Result> RevocarSesionAsync(Guid idSesion, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Sesion>>> ObtenerSesionesActivasPorUsuarioAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Sesion>>> ObtenerSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<int>> RevocarTodasPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<int>> ContarSesionesActivasAsync(int idUsuario, int idTenant, CancellationToken ct = default);
    Task<Result<int>> ContarSesionesActivasPorTenantAsync(int idTenant, CancellationToken ct = default);
    Task<Result<Sesion?>> ObtenerPorIdTokenExtAsync(string idTokenExt, CancellationToken ct = default);
    Task<Result<Sesion?>> ObtenerSesionActivaPorJtiAsync(int idUsuario, string jti, CancellationToken ct = default);
    Task<Result<Sesion?>> ObtenerPorHashRefreshAsync(string hashRefresh, CancellationToken ct = default);
    Task<Result<bool>> IntentarRotarHashRefreshAsync(Guid idSesion, string? hashRefreshEsperado, string? nuevoHashRefresh, DateTime nuevaFecExpira, CancellationToken ct = default);
    Task<Result<int>> DesactivarExpiradasAsync(CancellationToken ct = default);
}