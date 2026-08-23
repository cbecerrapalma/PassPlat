using System.Data;
using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using CBP.Results;
using PassPlat.Datos.Interfaces;
using PassPlat.Datos.SPResults;

namespace PassPlat.Datos.Repositories;

public class ExternalAuthRepository : IExternalAuthRepository
{
    private readonly IRawQueryRepositoryAsync _rawQuery;

    public ExternalAuthRepository(IUnitOfWorkAsync uow)
    {
        _rawQuery = uow.RawQuery;
    }

    public async Task<Result<LoginExternoResult>> LoginExternoAsync(int idTenant, int idApp, int idProvIden, string subExterno, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, string? metadataJson = null, string? ip = null, string? userAgent = null, int? idDisp = null, int? idAgente = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.Int("@IdProvIden", idProvIden),
            RawParameter.NVarChar("@SubExterno", subExterno, 255),
            RawParameter.NVarChar("@EmailExterno", emailExterno, 255),
            RawParameter.NVarChar("@NombreExterno", nombreExterno, 255),
            RawParameter.NVarChar("@Avatar", avatar, 500),
            RawParameter.NVarChar("@MetadataJson", metadataJson, -1),
            RawParameter.NVarChar("@IP", ip, 45),
            RawParameter.NVarChar("@UserAgent", userAgent, 500),
            RawParameter.Int("@IdDisp", idDisp),
            RawParameter.Int("@IdAgente", idAgente)
        };

        return await SpHelper.ExecuteSPAsync<LoginExternoResult>(_rawQuery, "SP_Auth_LoginExterno", parameters, "Sin resultado del login externo", ct);
    }

    public async Task<Result> RegistrarAuditoriaAsync(
        int idTenant, int idProvIden, int? idUsuario, string? subExterno, string evento, string resultado,
        string? detalle = null, string? ip = null, string? userAgent = null, string? correlationId = null,
        string? traceId = null, Guid? sessionId = null, string? refreshTokenId = null, string? jwtId = null,
        int? httpStatus = null, int? tiempoRespuesta = null, string? scopes = null, string? metodoAutenticacion = null,
        string? tipoLogin = null, string? origen = null, string? destino = null, string? codigo = null,
        string? excepcion = null, string? stackResumido = null, int? idDevice = null, string? browser = null, string? os = null,
        CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdProvIden", idProvIden),
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.NVarChar("@SubExterno", subExterno, 255),
            RawParameter.NVarChar("@Evento", evento, 100),
            RawParameter.NVarChar("@Resultado", resultado, 50),
            RawParameter.NVarChar("@Detalle", detalle, -1),
            RawParameter.NVarChar("@IP", ip, 45),
            RawParameter.NVarChar("@UserAgent", userAgent, 500),
            RawParameter.NVarChar("@CorrelationId", correlationId, 50),
            // ETAPA 12: campos extendidos
            RawParameter.NVarChar("@TraceId", traceId, 50),
            RawParameter.In("@SessionId", sessionId, DbType.Guid),
            RawParameter.NVarChar("@RefreshTokenId", refreshTokenId, 100),
            RawParameter.NVarChar("@JwtId", jwtId, 100),
            RawParameter.Int("@HttpStatus", httpStatus),
            RawParameter.Int("@TiempoRespuesta", tiempoRespuesta),
            RawParameter.NVarChar("@Scopes", scopes, 1000),
            RawParameter.NVarChar("@MetodoAutenticacion", metodoAutenticacion, 50),
            RawParameter.NVarChar("@TipoLogin", tipoLogin, 50),
            RawParameter.NVarChar("@Origen", origen, 500),
            RawParameter.NVarChar("@Destino", destino, 500),
            RawParameter.NVarChar("@Codigo", codigo, 50),
            RawParameter.NVarChar("@Excepcion", excepcion, -1),
            RawParameter.NVarChar("@StackResumido", stackResumido, -1),
            RawParameter.Int("@IdDevice", idDevice),
            RawParameter.NVarChar("@Browser", browser, 200),
            RawParameter.NVarChar("@OS", os, 200)
        };

        var result = await _rawQuery.QuerySPAsync<AuditoriaRegistradaResult>("SP_ProvIden_RegistrarAuditoria", parameters, ct);
        return result.IsFailure
            ? Result.Failure(result.Error!)
            : Result.Success();
    }

    public async Task<Result<long>> VincularUsuarioAsync(int idUsuario, int idProvIden, int idTenant, string subExterno, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, bool guardarTokens = false, string? metadataJson = null, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdProvIden", idProvIden),
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.NVarChar("@SubExterno", subExterno, 255),
            RawParameter.NVarChar("@EmailExterno", emailExterno, 255),
            RawParameter.NVarChar("@NombreExterno", nombreExterno, 255),
            RawParameter.NVarChar("@Avatar", avatar, 500),
            RawParameter.Bit("@GuardarTokens", guardarTokens),
            RawParameter.NVarChar("@MetadataJson", metadataJson, -1)
        };

        var result = await _rawQuery.QuerySPAsync<IdResult>("SP_ProvIden_VincularUsuario", parameters, ct);
        if (result.IsFailure)
            return Result<long>.Failure(result.Error!);

        var idResult = result.Value.FirstOrDefault();
        return idResult != null
            ? Result<long>.Success(idResult.Id)
            : Result<long>.Failure("SP_NO_RESULT", "Sin resultado del SP de vinculación");
    }

    public async Task<Result> ActualizarPerfilAsync(long idIdentidad, string? emailExterno = null, string? nombreExterno = null, string? avatar = null, string? metadataJson = null, bool guardarTokens = false, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.BigInt("@IdIdentidad", idIdentidad),
            RawParameter.NVarChar("@EmailExterno", emailExterno, 255),
            RawParameter.NVarChar("@NombreExterno", nombreExterno, 255),
            RawParameter.NVarChar("@Avatar", avatar, 500),
            RawParameter.NVarChar("@MetadataJson", metadataJson, -1),
            RawParameter.Bit("@GuardarTokens", guardarTokens)
        };

        var result = await _rawQuery.QuerySPAsync<FilasAfectadasResult>("SP_ProvIden_ActualizarPerfil", parameters, ct);
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }

    public async Task<Result> DesvincularIdentidadAsync(long idIdentidad, int idUsuarioElimina, bool revocarSesiones = true, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            RawParameter.BigInt("@IdIdentidad", idIdentidad),
            RawParameter.Int("@IdUsuarioElimina", idUsuarioElimina),
            RawParameter.Bit("@RevocarSesiones", revocarSesiones)
        };

        var result = await _rawQuery.QuerySPAsync<FilasAfectadasResult>("SP_IdenExt_Desvincular", parameters, ct);
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }
}

internal class AuditoriaRegistradaResult
{
    public long Id { get; set; }
}

internal class IdResult
{
    public long Id { get; set; }
}

internal class FilasAfectadasResult
{
    public int FilasAfectadas { get; set; }
}
