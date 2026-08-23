using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TipAsigPermisoController : ControllerBase
{
    private readonly ITipAsigPermisoService _service;

    public TipAsigPermisoController(ITipAsigPermisoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.ObtenerTodosAsync(ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message ?? "Error" });
        return Ok(result.Value);
    }
}
