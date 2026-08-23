using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/confproviden")]
public class ConfProvIdenController : BaseApiController
{
    private readonly IConfProvIdenService _service;
    private readonly IUnitOfWorkAsync _uow;

    public ConfProvIdenController(IConfProvIdenService service, IUnitOfWorkAsync uow)
    { _service = service; _uow = uow; }

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));

    [HttpGet("{idTenant}/{idProvIden}")]
    public async Task<IActionResult> ObtenerConfiguracion(int idTenant, int idProvIden, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerConfiguracionAsync(idTenant, idProvIden, ct));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] CBP.WebApi.Models.PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<ConfProvIden>(request.PageNumber, request.PageSize);
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearConfProvIdenDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ObtenerConfiguracion), new { idTenant = result.Value.IdTenant, idProvIden = result.Value.IdProvIden }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarConfProvIdenDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
}
