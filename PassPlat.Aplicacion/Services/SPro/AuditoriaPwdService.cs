using AutoMapper;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IAuditoriaPwdService : IServiceAsync<AuditoriaPwd, AuditoriaPwdDto>
{
    Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorUsuarioAsync(int idUsuario, int cantidad, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerEventosAltoRiesgoAsync(int nivelMinimo, int cantidad, CancellationToken ct = default);
    Task<Result<AuditoriaPwdDto>> RegistrarAuditoriaAsync(RegistrarAuditoriaPwdDto dto, CancellationToken ct = default);
}

public class AuditoriaPwdService : ServiceAsync<AuditoriaPwd, AuditoriaPwdDto>, IAuditoriaPwdService
{
    private readonly AuditoriaPwdRepository _repo;

    public AuditoriaPwdService(AuditoriaPwdRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorUsuarioAsync(int idUsuario, int cantidad, CancellationToken ct = default)
    {
        var auditoriaResult = await _repo.ObtenerPorUsuarioAsync(idUsuario, cantidad, ct);
        if (auditoriaResult.IsFailure) return Result<IReadOnlyList<AuditoriaPwdDto>>.Failure(auditoriaResult.Error!);
        var auditoria = auditoriaResult.Value;
        return Result<IReadOnlyList<AuditoriaPwdDto>>.Success(Mapper.Map<IReadOnlyList<AuditoriaPwdDto>>(auditoria));
    }

    public async Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default)
    {
        var auditoriaResult = await _repo.ObtenerPorTenantAsync(idTenant, cantidad, ct);
        if (auditoriaResult.IsFailure) return Result<IReadOnlyList<AuditoriaPwdDto>>.Failure(auditoriaResult.Error!);
        var auditoria = auditoriaResult.Value;
        return Result<IReadOnlyList<AuditoriaPwdDto>>.Success(Mapper.Map<IReadOnlyList<AuditoriaPwdDto>>(auditoria));
    }

    public async Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerEventosAltoRiesgoAsync(int nivelMinimo, int cantidad, CancellationToken ct = default)
    {
        var auditoriaResult = await _repo.ObtenerEventosAltoRiesgoAsync(nivelMinimo, cantidad, ct);
        if (auditoriaResult.IsFailure) return Result<IReadOnlyList<AuditoriaPwdDto>>.Failure(auditoriaResult.Error!);
        var auditoria = auditoriaResult.Value;
        return Result<IReadOnlyList<AuditoriaPwdDto>>.Success(Mapper.Map<IReadOnlyList<AuditoriaPwdDto>>(auditoria));
    }

    public async Task<Result<AuditoriaPwdDto>> RegistrarAuditoriaAsync(RegistrarAuditoriaPwdDto dto, CancellationToken ct = default)
    {
        var auditoriaResult = _repo.RegistrarAuditoria(dto.IdUsuario, dto.IdTipoAccion, dto.IdTenant, dto.IdApp, dto.IdUsrEjecutor, dto.IdDisp, dto.IdAgente, dto.IdIP, dto.IdHistPwd, dto.Detalles, dto.NivelRiesgo, dto.Metadata);
        if (auditoriaResult.IsFailure) return Result<AuditoriaPwdDto>.Failure(auditoriaResult.Error!);
        return Result<AuditoriaPwdDto>.Success(Mapper.Map<AuditoriaPwdDto>(auditoriaResult.Value));
    }
}
