using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "APPS_VER")]
public class AppsModulosController : BaseApiController
{
    private readonly IAppModuloService _service;
    private readonly IUnitOfWorkAsync _uow;

    public AppsModulosController(IAppModuloService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("app/{idApp}")] public async Task<IActionResult> GetPorApp(int idApp, CancellationToken ct) => FromResultQuery(await _service.ObtenerActivosPorAppAsync(idApp, ct));

    [HttpPost]
    [Authorize(Policy = "APPS_EDITAR")]
    public async Task<IActionResult> Create([FromBody] CrearAppModuloDto dto, CancellationToken ct)
    {
        var result = await _service.AsignarModuloAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPorApp), new { idApp = dto.IdApp }, result.Value);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "APPS_EDITAR")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var result = await _service.DesasignarModuloAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
