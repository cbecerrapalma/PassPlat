using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "SystemOnly")]
[Authorize(Policy = "MANTENIMIENTO_VER")]
[EnableRateLimiting("PurgePolicy")]
public class MaintenanceController : BaseApiController
{
    private readonly IMaintenanceService _service;
    public MaintenanceController(IMaintenanceService service) => _service = service;

    [HttpPost("purge")]
    public async Task<IActionResult> Purge([FromQuery] int diasRetencion = 365, CancellationToken ct = default)
    {
        if (diasRetencion < 30) return BadRequest(new { codigo = "RETENCION_MINIMA", mensaje = "La retencion minima es 30 dias" });
        if (diasRetencion > 730) return BadRequest(new { codigo = "RETENCION_MAXIMA", mensaje = "La retencion maxima es 730 dias" });
        var result = await _service.PurgeDatosAntiguosAsync(diasRetencion, ct);
        return FromResult(result);
    }
}
