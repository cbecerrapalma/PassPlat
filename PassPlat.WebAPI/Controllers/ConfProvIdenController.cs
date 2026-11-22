using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "FED_CONFPROVIDEN_VER")] // S114 · D1
[Route("api/confproviden")]
public class ConfProvIdenController : BaseApiController
{
    private readonly IConfProvIdenService _service;
    private readonly ITenantContext _tenantContext;

    // S122 · D7 — el servicio es el único dueño del commit (AGENTS §6)
    public ConfProvIdenController(IConfProvIdenService service, ITenantContext tenantContext)
    { _service = service; _tenantContext = tenantContext; }

    // S95 · D2 — un no-sistema solo lee su tenant; is_system lee todos o el tenant pedido
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(int idTenant) => User.FueraDeAlcance(_tenantContext, idTenant);

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    }

    [HttpGet("{idTenant}/{idProvIden}")]
    public async Task<IActionResult> ObtenerConfiguracion(int idTenant, int idProvIden, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerConfiguracionAsync(idTenant, idProvIden, ct));
    }

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] CBP.WebApi.Models.PaginationRequest request, [FromQuery] string? search, [FromQuery] int? idTenant, CancellationToken ct)
    {
        // S95 · D3 — search filtra en la consulta; un no-sistema queda en su tenant aunque mande idTenant
        var result = await _service.BuscarPaginadoAsync(AlcanceTenant ?? idTenant, search, request.PageNumber, request.PageSize, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "FED_CONFPROVIDEN_EDITAR")]
    public async Task<IActionResult> Create([FromBody] CrearConfProvIdenDto dto, CancellationToken ct)
    {
        // S99 · D4 — un no-sistema solo da de alta en su tenant
        if (FueraDeAlcance(dto.IdTenant)) return NotFound();
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedAtAction(nameof(ObtenerConfiguracion), new { idTenant = result.Value.IdTenant, idProvIden = result.Value.IdProvIden }, result.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "FED_CONFPROVIDEN_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarConfProvIdenDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, AlcanceTenant, ct);
        return FromResult(result);
    }

    [HttpPost("{id}/desactivar")]
    [Authorize(Policy = "FED_CONFPROVIDEN_EDITAR")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, AlcanceTenant, ct);
        return FromResult(result);
    }

    // S122 · D1 — reactiva una configuración desactivada
    [HttpPost("{id}/activar")]
    [Authorize(Policy = "FED_CONFPROVIDEN_EDITAR")]
    public async Task<IActionResult> Activar(int id, CancellationToken ct)
    {
        var result = await _service.ActivarAsync(id, AlcanceTenant, ct);
        return FromResult(result);
    }
}
