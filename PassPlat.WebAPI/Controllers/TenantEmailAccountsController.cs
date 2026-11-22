using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_TENANT_ACCTS_VER")]
public class TenantEmailAccountsController : BaseApiController
{
    private readonly ITenantEmailAccountService _service;
    private readonly ITenantContext _tenantContext;

    public TenantEmailAccountsController(ITenantEmailAccountService service, ITenantContext tenantContext)
    { _service = service; _tenantContext = tenantContext; }

    // S101 · D3 (criterio S95) — un no-sistema solo lee y escribe su tenant; fuera de él, 404
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(int idTenant) => User.FueraDeAlcance(_tenantContext, idTenant);

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result.IsSuccess && result.Value is not null && FueraDeAlcance(result.Value.IdTenant)) return NotFound();
        return FromResultQuery(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearTenantEmailAccountDto dto, CancellationToken ct)
    {
        if (FueraDeAlcance(dto.IdTenant)) return NotFound();
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedFromResult(nameof(GetById), new { id = result.Value.Id }, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return NoContent();
    }
}
