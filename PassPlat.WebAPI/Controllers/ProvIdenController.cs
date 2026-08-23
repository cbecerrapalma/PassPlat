using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/providen")]
public class ProvIdenController : BaseApiController
{
    private readonly IProvIdenService _service;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ITenantContext _tenantContext;

    public ProvIdenController(IProvIdenService service, IUnitOfWorkAsync uow, ITenantContext tenantContext)
    { _service = service; _uow = uow; _tenantContext = tenantContext; }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResultQuery(await _service.GetAllAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResultQuery(await _service.GetByIdAsync(id, ct));

    [HttpGet("{id}/info")]
    public async Task<IActionResult> GetProviderInfo(int id, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerInfoConfiguracionAsync(id, ct));

    [HttpGet("codigo/{codigo}")]
    public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorCodigoAsync(codigo, ct));

    [HttpGet("activos")]
    public async Task<IActionResult> GetActivos(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerActivosAsync(ct));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerCatalogAsync(_tenantContext.CurrentId, ct));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<ProvIden>(request.PageNumber, request.PageSize);
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearProvIdenDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarProvIdenDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
}
