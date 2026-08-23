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
public class UsuariosPermisosController : ControllerBase
{
    private readonly IUsuarioPermisoService _service;
    private readonly IUnitOfWorkAsync _uow;

    public UsuariosPermisosController(IUsuarioPermisoService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
    {
        var result = await _service.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Conceder([FromBody] CrearUsuarioPermisoDto dto, CancellationToken ct)
    {
        var idUsrEjecutor = ObtenerIdUsuario();
        var result = await _service.ConcederPermisoAsync(dto, idUsrEjecutor, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });

        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetByUsuario), new { idUsuario = dto.IdUsuario }, result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Revocar(int id, CancellationToken ct)
    {
        var result = await _service.RevocarPermisoAsync(id, ct);
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
