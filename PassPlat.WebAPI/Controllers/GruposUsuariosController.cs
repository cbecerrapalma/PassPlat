using CBP.Data.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GruposUsuariosController : ControllerBase
{
    private readonly IGrupoUsuarioService _service;
    private readonly IUnitOfWorkAsync _uow;

    public GruposUsuariosController(IGrupoUsuarioService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("grupo/{idGrupo}")]
    public async Task<IActionResult> GetByGrupo(int idGrupo, CancellationToken ct)
    {
        var result = await _service.ObtenerPorGrupoAsync(idGrupo, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
    {
        var result = await _service.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Agregar([FromBody] CrearGrupoUsuarioDto dto, CancellationToken ct)
    {
        var idUsrMod = ObtenerIdUsuario();
        var result = await _service.AgregarMiembroAsync(dto, idUsrMod, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByGrupo), new { idGrupo = dto.IdGrupo }, result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        var result = await _service.RemoverMiembroAsync(id, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return NoContent();
    }

    private int ObtenerIdUsuario()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
