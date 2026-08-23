using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.Datos.SPResults;

namespace PassPlat.Aplicacion.Services;

public interface ITokenRestService : IServiceAsync<TokenRest, TokenRestDto>
{
    Task<Result<GenerarTokenResult>> GenerarTokenAsync(int idUsuario, int idTenant, int idApp, string hashToken, DateTime fecVence, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
    Task<Result<ValidarTokenResult>> ValidarTokenAsync(string hashToken, int? idApp, CancellationToken ct = default);
    Task<Result<GenerarTokenResult>> GenerarTokenResetPasswordAsync(int idUsuario, int idTenant, int idApp, string tokenPlano, string hashToken, DateTime fecVence, string baseUrl, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default);
}

public class TokenRestService : ServiceAsync<TokenRest, TokenRestDto>, ITokenRestService
{
    private readonly TokenRestRepository _repo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<TokenRestService> _logger;

    public TokenRestService(TokenRestRepository repo, IMapper mapper, IUsuarioRepository usuarioRepo, IEmailQueue emailQueue, ILogger<TokenRestService> logger)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<Result<GenerarTokenResult>> GenerarTokenAsync(int idUsuario, int idTenant, int idApp, string hashToken, DateTime fecVence, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var result = await _repo.GenerarTokenAsync(idUsuario, idTenant, idApp, hashToken, fecVence, idDisp, idIP, idAgente, ct);
        return result;
    }

    public async Task<Result<ValidarTokenResult>> ValidarTokenAsync(string hashToken, int? idApp, CancellationToken ct = default)
    {
        var result = await _repo.ValidarTokenAsync(hashToken, idApp, ct);
        return result;
    }

    public async Task<Result<GenerarTokenResult>> GenerarTokenResetPasswordAsync(int idUsuario, int idTenant, int idApp, string tokenPlano, string hashToken, DateTime fecVence, string baseUrl, int? idDisp, int? idIP, int? idAgente, CancellationToken ct = default)
    {
        var result = await _repo.GenerarTokenAsync(idUsuario, idTenant, idApp, hashToken, fecVence, idDisp, idIP, idAgente, ct);
        if (result.IsFailure)
            return result;

        await NotificarResetPasswordAsync(idUsuario, tokenPlano, baseUrl, idTenant, idApp, ct);
        return result;
    }

    private async Task NotificarResetPasswordAsync(int idUsuario, string tokenPlano, string baseUrl, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return;
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return;

            var resetLink = $"{baseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(tokenPlano)}";
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.PasswordReset,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["ResetLink"] = resetLink },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación de reset de password para usuario {IdUsuario}", idUsuario);
        }
    }
}
