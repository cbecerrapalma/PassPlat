using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "CONFIG_APP_VER")]
public class ConfigTenantsController : BaseApiController
{
    private readonly IConfigTenantService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public ConfigTenantsController(IConfigTenantService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("tenant")] public async Task<IActionResult> GetByTenant(CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId));
    [HttpGet("tenant/{idTenant:int}")] public async Task<IActionResult> GetByTenantId(int idTenant, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    [HttpPost("{id}/pepper-version")] public async Task<IActionResult> ActualizarPepperVersion(int id, [FromBody] byte version, CancellationToken ct)
    {
        var result = await _service.ActualizarPepperVersionAsync(id, version, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpGet("count")] public async Task<IActionResult> Count(CancellationToken ct) => FromResultQuery(await _service.CountAsync(ct));
}