using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EmailLogController : ControllerBase
{
    private readonly IEmailLogService _service;

    public EmailLogController(IEmailLogService service) => _service = service;

    [HttpGet("pendientes")]
    public async Task<IActionResult> GetPendientes(CancellationToken ct)
    {
        var result = await _service.ObtenerPendientesAsync(ct);
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
}
