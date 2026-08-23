using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "TENANTS_VER")]
public class TenantsController : BaseApiController
{
    private readonly ITenantService _service;
    private readonly IUnitOfWorkAsync _uow;

    public TenantsController(ITenantService service, IUnitOfWorkAsync uow)
    { _service = service; _uow = uow; }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerTodosAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorIdAsync(id));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, CancellationToken ct)
    {
        var options = new PaginationOptions<Tenant>(request.PageNumber, request.PageSize);
        if (!string.IsNullOrWhiteSpace(search))
            return FromResultQuery(await _service.ObtenerPaginadoConBusquedaAsync(options, search, ct));
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpGet("codigo/{codigo}")]
    public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorCodigoAsync(codigo));

    [HttpGet("activos")]
    public async Task<IActionResult> GetActivos(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerActivosAsync());

    [HttpGet("activos/count")]
    public async Task<IActionResult> CountActivos(CancellationToken ct)
        => FromResultQuery(await _service.CountActivosAsync(ct));

    [HttpPost]
    [Authorize(Policy = "TENANTS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearTenantDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count(CancellationToken ct)
        => FromResultQuery(await _service.CountAsync(ct));

    [HttpPut("{id}")]
    [Authorize(Policy = "TENANTS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarTenantDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("{id}/desactivar")]
    [Authorize(Policy = "TENANTS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Problem($"Error al desactivar tenant: {ex.InnerException?.Message ?? ex.Message}", statusCode: 500);
        }
        return FromResult(result);
    }
}
