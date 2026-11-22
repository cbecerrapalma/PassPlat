using AutoMapper;
using CBP.Data.Abstractions;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.Services.Async;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Datos;
using PassPlat.Datos.Models;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Services;

public interface IAuditoriaPwdService : IServiceAsync<AuditoriaPwd, AuditoriaPwdDto>
{
    Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default);
    Task<Result<AuditoriaPwdDto>> RegistrarAuditoriaAsync(RegistrarAuditoriaPwdDto dto, CancellationToken ct = default);
    Task<Result<IPagedResult<AuditoriaPwdDto>>> BuscarAsync(FiltroAuditoriaPwd filtro, int pageNumber, int pageSize, CancellationToken ct = default);
}

public class AuditoriaPwdService : ServiceAsync<AuditoriaPwd, AuditoriaPwdDto>, IAuditoriaPwdService
{
    private readonly AuditoriaPwdRepository _repo;

    public AuditoriaPwdService(AuditoriaPwdRepository repo, IMapper mapper)
        : base(repo, mapper) => _repo = repo;

    public async Task<Result<IReadOnlyList<AuditoriaPwdDto>>> ObtenerPorTenantAsync(int idTenant, int cantidad, CancellationToken ct = default)
    {
        var auditoriaResult = await _repo.ObtenerPorTenantAsync(idTenant, cantidad, ct);
        if (auditoriaResult.IsFailure) return Result<IReadOnlyList<AuditoriaPwdDto>>.Failure(auditoriaResult.Error!);
        var auditoria = auditoriaResult.Value;
        return Result<IReadOnlyList<AuditoriaPwdDto>>.Success(Mapper.Map<IReadOnlyList<AuditoriaPwdDto>>(auditoria));
    }

    // S95 · D1 — búsqueda paginada; página o tamaño inválidos → 1 y 20
    public async Task<Result<IPagedResult<AuditoriaPwdDto>>> BuscarAsync(FiltroAuditoriaPwd filtro, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var pagina = pageNumber < 1 ? 1 : pageNumber;
        var tamano = pageSize < 1 ? 20 : pageSize;
        var pagResult = await _repo.BuscarAsync(filtro, pagina, tamano, ct);
        if (pagResult.IsFailure) return Result<IPagedResult<AuditoriaPwdDto>>.Failure(pagResult.Error!);
        var (items, totalCount) = pagResult.Value;
        var mapped = Mapper.Map<IReadOnlyList<AuditoriaPwdDto>>(items);
        return Result<IPagedResult<AuditoriaPwdDto>>.Success(
            new PagedResultDto<AuditoriaPwdDto>(mapped, totalCount, pagina, tamano));
    }

    public async Task<Result<AuditoriaPwdDto>> RegistrarAuditoriaAsync(RegistrarAuditoriaPwdDto dto, CancellationToken ct = default)
    {
        var auditoriaResult = _repo.RegistrarAuditoria(dto.IdUsuario, dto.IdTipoAccion, dto.IdTenant, dto.IdApp, dto.IdUsrEjecutor, dto.IdDisp, dto.IdAgente, dto.IdIP, dto.IdHistPwd, dto.Detalles, dto.NivelRiesgo, dto.Metadata);
        if (auditoriaResult.IsFailure) return Result<AuditoriaPwdDto>.Failure(auditoriaResult.Error!);
        return Result<AuditoriaPwdDto>.Success(Mapper.Map<AuditoriaPwdDto>(auditoriaResult.Value));
    }
}
