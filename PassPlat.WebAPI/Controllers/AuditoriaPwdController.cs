using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Services.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "AUDITORIA_VER")]
public class AuditoriaPwdController : BaseApiController
{
    private readonly IAuditoriaPwdService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public AuditoriaPwdController(IAuditoriaPwdService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id));
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<AuditoriaPwd>(request.PageNumber, request.PageSize);
        var result = User.HasClaim("is_system", "true")
            ? await _service.GetPagedAsync(options, ct)
            : await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("page/tenant/{idTenant}")] public async Task<IActionResult> GetPagedByTenant(int idTenant, [FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<AuditoriaPwd>(request.PageNumber, request.PageSize);
        var repoResult = await _service.ObtenerPorTenantAsync(idTenant, request.PageSize, ct);
        if (repoResult.IsFailure) return FromResult(repoResult);
        var items = repoResult.Value;
        var total = items.Count;
        var paged = items.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        return Ok(ToPagedResponse(new PagedResultDto<AuditoriaPwdDto>(paged, total, request.PageNumber, request.PageSize)));
    }
    [HttpGet("usuario/{idUsuario}/{cantidad}")] public async Task<IActionResult> GetByUsuario(int idUsuario, int cantidad, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, cantidad));
    [HttpGet("tenant/{cantidad}")] public async Task<IActionResult> GetByTenant(int cantidad, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, cantidad));
    [HttpGet("alto-riesgo/{nivelMinimo}/{cantidad}")] public async Task<IActionResult> GetAltoRiesgo(int nivelMinimo, int cantidad, CancellationToken ct) => FromResultQuery(await _service.ObtenerEventosAltoRiesgoAsync(nivelMinimo, cantidad));
    [HttpPost("registrar")] public async Task<IActionResult> Registrar([FromBody] RegistrarAuditoriaPwdDto dto, CancellationToken ct)
    {
        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.RegistrarAuditoriaAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
}
