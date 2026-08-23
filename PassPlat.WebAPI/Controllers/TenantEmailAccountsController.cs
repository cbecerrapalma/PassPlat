using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_TENANT_ACCOUNTS_VER")]
public class TenantEmailAccountsController : BaseApiController
{
    private readonly ITenantEmailAccountService _service;
    public TenantEmailAccountsController(ITenantEmailAccountService service) => _service = service;

    [HttpGet("tenant/{idTenant}")] public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearTenantEmailAccountDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        return NoContent();
    }
}
