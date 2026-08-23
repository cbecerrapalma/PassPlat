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

[Authorize(Policy = "APPS_VER")]
public class AppsController : BaseApiController
{
    private readonly IAppService _service;
    private readonly IUnitOfWorkAsync _uow;
    public AppsController(IAppService service, IUnitOfWorkAsync uow)
    { _service = service; _uow = uow; }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodasAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, CancellationToken ct)
    {
        var options = new PaginationOptions<App>(request.PageNumber, request.PageSize);
        if (!string.IsNullOrWhiteSpace(search))
            return FromResultQuery(await _service.ObtenerPaginadoConBusquedaAsync(options, search, ct));
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("codigo/{codigo}")] public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorCodigoAsync(codigo));
    [AllowAnonymous]
    [HttpGet("activas")] public async Task<IActionResult> GetActivas(CancellationToken ct) => FromResultQuery(await _service.ObtenerActivasAsync());
    [HttpPost]
    [Authorize(Policy = "APPS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearAppDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
    [HttpPost("{id}/desactivar")]
    [Authorize(Policy = "APPS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
    [HttpGet("count")] public async Task<IActionResult> Count(CancellationToken ct) => FromResultQuery(await _service.CountAsync(ct));
}