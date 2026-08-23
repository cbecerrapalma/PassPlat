using AutoMapper;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using Microsoft.Extensions.Logging;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IIntentoAccesoService : IServiceAsync<IntentoAcceso, IntentoAccesoDto>
{
    Task<Result<IReadOnlyList<IntentoAccesoDto>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default);
    Task<Result<int>> ContarIntentosFallidosPorIPAsync(int idIP, int minutos, CancellationToken ct = default);
    Task<Result<IntentoAccesoDto>> RegistrarIntentoAsync(RegistrarIntentoAccesoDto dto, CancellationToken ct = default);
}

public class IntentoAccesoService : ServiceAsync<IntentoAcceso, IntentoAccesoDto>, IIntentoAccesoService
{
    private const int AlertaUmbral = 3;
    private const int VentanaMinutos = 15;

    private readonly IntentoAccesoRepository _repo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IEmailQueue _emailQueue;
    private readonly IntentoAccesoRepository _intentoRepo;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<IntentoAccesoService> _logger;

    public IntentoAccesoService(IntentoAccesoRepository repo, IMapper mapper, IUsuarioRepository usuarioRepo, IEmailQueue emailQueue, ITenantContext tenantContext, ILogger<IntentoAccesoService> logger)
        : base(repo, mapper)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _emailQueue = emailQueue;
        _intentoRepo = repo;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public override async Task<Result<IPagedResult<IntentoAccesoDto>>> GetPagedAsync(
        PaginationOptions<IntentoAcceso> options, CancellationToken ct = default)
    {
        var pagedResult = await _repo.GetPagedWithIncludesAsync(
            options.PageNumber, options.PageSize, _tenantContext.CurrentId, options.IncludeTotalCount, ct);
        if (pagedResult.IsFailure) return Result<IPagedResult<IntentoAccesoDto>>.Failure(pagedResult.Error!);
        var (items, totalCount) = pagedResult.Value;
        var dtos = Mapper.Map<IReadOnlyList<IntentoAccesoDto>>(items);
        var result = new PagedResultDto<IntentoAccesoDto>(dtos, totalCount, options.PageNumber, options.PageSize);
        return Result<IPagedResult<IntentoAccesoDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<IntentoAccesoDto>>> ObtenerIntentosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        var intentosResult = await _repo.ObtenerIntentosRecientesAsync(idUsuario, minutos, ct);
        if (intentosResult.IsFailure) return Result<IReadOnlyList<IntentoAccesoDto>>.Failure(intentosResult.Error!);
        var intentos = intentosResult.Value;
        return Result<IReadOnlyList<IntentoAccesoDto>>.Success(Mapper.Map<IReadOnlyList<IntentoAccesoDto>>(intentos));
    }

    public async Task<Result<int>> ContarIntentosFallidosRecientesAsync(int idUsuario, int minutos, CancellationToken ct = default)
    {
        var result = await _repo.ContarIntentosFallidosRecientesAsync(idUsuario, minutos, ct);
        if (result.IsFailure) return Result<int>.Failure(result.Error!);
        return Result<int>.Success(result.Value);
    }

    public async Task<Result<int>> ContarIntentosFallidosPorIPAsync(int idIP, int minutos, CancellationToken ct = default)
    {
        var result = await _repo.ContarIntentosFallidosPorIPAsync(idIP, minutos, ct);
        if (result.IsFailure) return Result<int>.Failure(result.Error!);
        return Result<int>.Success(result.Value);
    }

    public async Task<Result<IntentoAccesoDto>> RegistrarIntentoAsync(RegistrarIntentoAccesoDto dto, CancellationToken ct = default)
    {
        var intentoResult = _repo.RegistrarIntento(dto.IdResultado, dto.NomUsuarioIntentado, dto.Exitoso, dto.IdUsuario, dto.IdTenant, dto.IdApp, dto.IdDisp, dto.IdAgente, dto.IdIP, dto.DetResultado, dto.TpoRespuesta, dto.CodRespuesta);
        if (intentoResult.IsFailure) return Result<IntentoAccesoDto>.Failure(intentoResult.Error!);

        if (!dto.Exitoso && dto.IdUsuario.HasValue)
            await VerificarAlertaSeguridadAsync(dto.IdUsuario.Value, dto.NomUsuarioIntentado, dto.IdIP, dto.IdTenant, dto.IdApp, ct);

        return Result<IntentoAccesoDto>.Success(Mapper.Map<IntentoAccesoDto>(intentoResult.Value));
    }

    private async Task VerificarAlertaSeguridadAsync(int idUsuario, string nomUsuario, int? idIP, int? idTenant = null, int? idApp = null, CancellationToken ct = default)
    {
        try
        {
            var countResult = await _repo.ContarIntentosFallidosRecientesAsync(idUsuario, VentanaMinutos, ct);
            if (countResult.IsFailure) return;
            var count = countResult.Value;
            if (count + 1 < AlertaUmbral)
                return;

            var usuarioResult = await _usuarioRepo.ObtenerPorIdAsync(idUsuario, ct);
            if (usuarioResult.IsFailure) return;
            var usuario = usuarioResult.Value;
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return;

            var alertMsg = $"Se detectaron {count} intentos de inicio de sesión fallidos en los últimos {VentanaMinutos} minutos.";
            await _emailQueue.EnqueueAsync(new EmailJob(
                EmailJobKind.SecurityAlert,
                usuario.Email,
                usuario.NomUsuario,
                new Dictionary<string, object?>
                {
                    ["AlertMessage"] = alertMsg,
                    ["Ip"] = idIP?.ToString() ?? "Desconocida"
                },
                idTenant,
                usuario.Id,
                idApp,
                null), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar/encolar alerta de seguridad para usuario {IdUsuario}", idUsuario);
        }
    }
}
