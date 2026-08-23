using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/federacion")]
public class FederacionController : BaseApiController
{
    private readonly IFederacionService _federacionService;

    public FederacionController(IFederacionService federacionService)
    {
        _federacionService = federacionService;
    }

    [HttpGet("estadisticas/{idTenant}")]
    public async Task<IActionResult> ObtenerEstadisticas(int idTenant, CancellationToken ct)
    {
        var result = await _federacionService.ObtenerEstadisticasAsync(idTenant, ct);
        return FromResult(result);
    }
}
