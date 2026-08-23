using CBP.Results;
using PassPlat.Datos.SPResults;

namespace PassPlat.Datos.Interfaces;

/// <summary>
/// Repositorio SP-based para operaciones multi-tabla/complejas de federación.
/// CRUD individual (ProvIden, ConfProvIden, IdenExt) va por EF Core RepositoryAsync&lt;T&gt;.
/// </summary>
public interface IExternalAuthRepository
{
    // Login externo (multi-tabla: IdenExt + Usuarios + Accesos + MFA + IntentosAcceso)
    Task<Result<LoginExternoResult>> LoginExternoAsync(int idTenant, int idApp, int idProvIden, string subExterno, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, string? metadataJson = null, string? ip = null, string? userAgent = null, int? idDisp = null, int? idAgente = null, CancellationToken ct = default);

    // Auditoría (cross-cutting, siempre por SP) — ETAPA 12: campos extendidos
    Task<Result> RegistrarAuditoriaAsync(
        int idTenant, int idProvIden, int? idUsuario, string? subExterno, string evento, string resultado,
        string? detalle = null, string? ip = null, string? userAgent = null, string? correlationId = null,
        string? traceId = null, Guid? sessionId = null, string? refreshTokenId = null, string? jwtId = null,
        int? httpStatus = null, int? tiempoRespuesta = null, string? scopes = null, string? metodoAutenticacion = null,
        string? tipoLogin = null, string? origen = null, string? destino = null, string? codigo = null,
        string? excepcion = null, string? stackResumido = null, int? idDevice = null, string? browser = null, string? os = null,
        CancellationToken ct = default);

    // Vinculación (valida duplicados cruzados: mismo sub en otro usuario, mismo usuario con otro provider)
    Task<Result<long>> VincularUsuarioAsync(int idUsuario, int idProvIden, int idTenant, string subExterno, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, bool guardarTokens = false, string? metadataJson = null, CancellationToken ct = default);

    // Actualizar perfil (update condicional con guardarTokens)
    Task<Result> ActualizarPerfilAsync(long idIdentidad, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, string? metadataJson = null, bool guardarTokens = false, CancellationToken ct = default);

    // Desvincular (multi-tabla: IdenExt + Sesiones)
    Task<Result> DesvincularIdentidadAsync(long idIdentidad, int idUsuarioElimina, bool revocarSesiones = true, CancellationToken ct = default);
}
