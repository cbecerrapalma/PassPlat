using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GruposController : ControllerBase
{
    private readonly IGrupoService _service;
    private readonly IGrupoUsuarioService _grupoUsuarioService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public GruposController(IGrupoService service, IGrupoUsuarioService grupoUsuarioService, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _grupoUsuarioService = grupoUsuarioService;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.ObtenerTodosAsync(ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var gruposResult = await _service.ObtenerTodosAsync(ct);
        if (gruposResult.IsFailure) return BadRequest(new { error = gruposResult.Error?.Message ?? "Error" });

        var stats = new Dictionary<int, object>();
        foreach (var g in gruposResult.Value!)
        {
            var miembrosResult = await _grupoUsuarioService.ObtenerPorGrupoAsync(g.Id, ct);
            var count = miembrosResult.IsSuccess ? miembrosResult.Value!.Count : 0;
            var usuarios = miembrosResult.IsSuccess ? miembrosResult.Value!.Select(m => m.IdUsuario).Distinct().Count() : 0;
            stats[g.Id] = new { miembros = count, usuarios };
        }
        return Ok(stats);
    }

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
    {
        var result = await _service.ObtenerPorTenantAsync(idTenant, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpGet("codigo/{idTenant}/{codigo}")]
    public async Task<IActionResult> GetByCodigo(int idTenant, string codigo, CancellationToken ct)
    {
        var result = await _service.ObtenerPorCodigoAsync(idTenant, codigo, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        if (result.Value == null) return NotFound();
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearGrupoDto dto, CancellationToken ct)
    {
        dto.IdTenant = _tenantContext.CurrentId;

        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByTenant), new { idTenant = dto.IdTenant }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarGrupoDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return Ok(result.Value);
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
