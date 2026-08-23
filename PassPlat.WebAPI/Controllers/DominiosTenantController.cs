using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "TENANTS_VER")]
public class DominiosTenantController : BaseApiController
{
    private readonly IDominioTenantService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public DominiosTenantController(IDominioTenantService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("tenant")] public async Task<IActionResult> GetByTenant(CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId));
    [HttpGet("tenant/{idTenant:int}")] public async Task<IActionResult> GetByTenantId(int idTenant, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    [HttpPost] public async Task<IActionResult> AgregarDominio([FromBody] CrearDominioTenantDto dto, CancellationToken ct)
    {
        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.AgregarDominioAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetByTenant), new { idTenant = dto.IdTenant }, result.Value);
    }
    [HttpGet("existe/{dominio}")] public async Task<IActionResult> ExisteDominio(string dominio, CancellationToken ct) => Ok(await _service.ExisteDominioAsync(_tenantContext.CurrentId, dominio));
    [HttpGet("count")] public async Task<IActionResult> Count(CancellationToken ct) => FromResultQuery(await _service.CountAsync(ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CrearDominioTenantDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarDominioAsync(id, dto.Dominio, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var result = await _service.EliminarDominioAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}