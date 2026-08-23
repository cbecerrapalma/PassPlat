using CBP.Data.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RolesHerenciaController : ControllerBase
{
    private readonly IRolesHerenciaService _service;
    private readonly IUnitOfWorkAsync _uow;

    public RolesHerenciaController(IRolesHerenciaService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
    {
        var result = await _service.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpGet("hijos/{idRolPadre}")]
    public async Task<IActionResult> GetHijos(int idRolPadre, CancellationToken ct)
    {
        var result = await _service.ObtenerHijosAsync(idRolPadre, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpGet("padres/{idRolHijo}")]
    public async Task<IActionResult> GetPadres(int idRolHijo, CancellationToken ct)
    {
        var result = await _service.ObtenerPadresAsync(idRolHijo, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearRolesHerenciaDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByTenant), new { idTenant = dto.IdTenant }, result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return NoContent();
    }
}
