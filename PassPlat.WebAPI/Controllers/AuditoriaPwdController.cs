using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.Results;
using CBP.Services.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Models;
using System.Security.Claims;

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

    // S95 · D2 — un no-sistema solo lee su tenant; is_system lee todos o el tenant pedido
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private IActionResult Lista(Result<IPagedResult<AuditoriaPwdDto>> result) => result.IsSuccess ? Ok(result.Value.Items) : FromResult(result);

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => Lista(await _service.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: AlcanceTenant), 1, int.MaxValue, ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id);
        if (result.IsSuccess && AlcanceTenant is int propio && result.Value?.IdTenant != propio) return NotFound();
        return FromResultQuery(result);
    }
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, [FromQuery] int? idTipoAccion, [FromQuery] int? nivelMinimo, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int? idTenant, CancellationToken ct)
    {
        // S95 · D1 — la consulta aplica los filtros; un no-sistema queda en su tenant aunque mande idTenant
        var result = await _service.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: AlcanceTenant ?? idTenant, IdTipoAccion: idTipoAccion, NivelMinimo: nivelMinimo, Desde: desde, Hasta: hasta, Texto: search), request.PageNumber, request.PageSize, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("usuario/{idUsuario}/{cantidad}")] public async Task<IActionResult> GetByUsuario(int idUsuario, int cantidad, CancellationToken ct) => Lista(await _service.BuscarAsync(new FiltroAuditoriaPwd(IdTenant: AlcanceTenant, IdUsuario: idUsuario), 1, cantidad, ct));
}
