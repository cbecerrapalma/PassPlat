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
using PassPlat.Dominio.Enums;

namespace PassPlat.Aplicacion.Services;

public interface IMFAService : IServiceAsync<MFA, MFADto>
{
    Task<Result<ValidarMFAResult>> ValidarMFAAsync(int idUsuario, int idTenant, int idTipoMFA, string idMFA, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MFADto>>> ObtenerMetodosPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<MFADto?>> ObtenerMetodoPrincipalAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<MFADto>> RegistrarMFAAsync(RegistrarMFADto dto, CancellationToken ct = default);
    Task<Result> RevocarMetodoAsync(int idUsuario, int idMFARegistro, int? idTenant = null, int? idApp = null, CancellationToken ct = default);
}

public class MFAService : ServiceAsync<MFA, MFADto>, IMFAService
{
    private readonly MFARepository _repo;
    private readonly IMfaCodeStore _mfaCodeStore;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<MFAService> _logger;

    public MFAService(MFARepository repo, IMapper mapper, IMfaCodeStore mfaCodeStore,
        IUsuarioRepository usuarioRepo, IEmailQueue emailQueue, ILogger<MFAService> logger)
        : base(repo, mapper)
    {
        _repo = repo;
        _mfaCodeStore = mfaCodeStore;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<Result<ValidarMFAResult>> ValidarEmailAsync(int idUsuario, int idTenant, string code, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return Result<ValidarMFAResult>.Failure(usuarioResult.Error!);
            var usuario = usuarioResult.Value;

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return Result<ValidarMFAResult>.Failure("NO_EMAIL", "El usuario no tiene un correo electrónico registrado");

            var valido = await _mfaCodeStore.ValidateAndConsumeAsync(idUsuario, idTenant, code, ct);
            return Result<ValidarMFAResult>.Success(new ValidarMFAResult
            {
                Exito = valido ? 1 : 0,
                EsPrincipal = valido
            });
        }
        catch (Exception ex)
        {
            return Result<ValidarMFAResult>.Failure("MFA_VALIDATE_ERROR", ex.Message);
        }
    }

    public async Task<Result<ValidarMFAResult>> ValidarMFAAsync(int idUsuario, int idTenant, int idTipoMFA, string idMFA, CancellationToken ct = default)
    {
        if (idTipoMFA == (int)ETipoMFA.Email)
        {
            var valido = await _mfaCodeStore.ValidateAndConsumeAsync(idUsuario, idTenant, idMFA, ct);
            return Result<ValidarMFAResult>.Success(new ValidarMFAResult
            {
                Exito = valido ? 1 : 0,
                EsPrincipal = valido
            });
        }

        var result = await _repo.ValidarMFAAsync(idUsuario, idTenant, idTipoMFA, idMFA, ct);
        return result;
    }

    public async Task<Result<MFADto>> RegistrarMFAAsync(RegistrarMFADto dto, CancellationToken ct = default)
    {
        var entity = Mapper.Map<MFA>(dto);
        entity.FecAlta = DateTime.Now;
        var addResult = Repository.Add(entity);
        if (addResult.IsFailure) return Result<MFADto>.Failure(addResult.Error!);

        await NotificarMFAAsync(dto.IdUsuario, EmailJobKind.MfaEnabled, dto.IdTenant, null, ct);
        return Result<MFADto>.Success(Mapper.Map<MFADto>(entity));
    }

    public async Task<Result<IReadOnlyList<MFADto>>> ObtenerMetodosPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var metodosResult = await _repo.ObtenerMetodosPorUsuarioAsync(idUsuario, ct);
        if (metodosResult.IsFailure) return Result<IReadOnlyList<MFADto>>.Failure(metodosResult.Error!);
        return Result<IReadOnlyList<MFADto>>.Success(Mapper.Map<IReadOnlyList<MFADto>>(metodosResult.Value));
    }

    public async Task<Result<MFADto?>> ObtenerMetodoPrincipalAsync(int idUsuario, CancellationToken ct = default)
    {
        var metodoResult = await _repo.ObtenerMetodoPrincipalAsync(idUsuario, ct);
        if (metodoResult.IsFailure) return Result<MFADto?>.Failure(metodoResult.Error!);
        return Result<MFADto?>.Success(Mapper.Map<MFADto?>(metodoResult.Value), allowNull: true);
    }

    public async Task<Result> RevocarMetodoAsync(int idUsuario, int idMFARegistro, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        var repoResult = _repo.RevocarMetodo(idUsuario, idMFARegistro);
        if (repoResult.IsFailure) return Result.Failure(repoResult.Error!);

        await NotificarMFAAsync(idUsuario, EmailJobKind.MfaDisabled, idTenant, idApp, ct);
        return Result.Success();
    }

    private async Task NotificarMFAAsync(int idUsuario, EmailJobKind kind, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure || usuarioResult.Value == null) return;
            var usuario = usuarioResult.Value;
            if (string.IsNullOrWhiteSpace(usuario.Email)) return;

            var tipoNombre = kind == EmailJobKind.MfaEnabled ? "activado" : "desactivado";
            await _emailQueue.EnqueueAsync(new EmailJob(
                kind,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?> { ["TipoAccion"] = tipoNombre, ["AppName"] = "PassPlat" },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar notificación MFA para usuario {IdUsuario}", idUsuario);
        }
    }
}
