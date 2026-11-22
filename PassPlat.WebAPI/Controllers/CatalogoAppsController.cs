using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

/// <summary>
/// S99 D2 — Contrato genérico de inyección del catálogo de una app: <c>POST api/apps/{codigoApp}/catalogo</c>.
/// Solo un usuario maestro (<c>is_system</c>) cuyo token es de esa misma app. Un solo <c>SaveChangesAsync</c>: sin estado parcial.
/// </summary>
[Authorize]
[Route("api/apps/{codigoApp}/catalogo")]
public class CatalogoAppsController : BaseApiController
{
    private readonly IInyeccionCatalogoService _service;
    private readonly IUnitOfWorkAsync _uow;

    public CatalogoAppsController(IInyeccionCatalogoService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpPost]
    public async Task<IActionResult> Inyectar(string codigoApp, [FromBody] InyeccionCatalogoDto catalogo, CancellationToken ct)
    {
        var esSistema = User.HasClaim("is_system", "true");
        int? idApp = int.TryParse(User.FindFirst("IdApp")?.Value, out var id) ? id : null;

        var result = await _service.InyectarAsync(codigoApp, catalogo, esSistema, idApp, ct);
        if (result.IsFailure)
        {
            if (result.Error!.Code != "CATALOGO_ESTADO_CAMBIO") return FromResult(result);

            // 409: el estado cambió desde el plan revisado; se devuelve el plan nuevo (simulado, sin escribir).
            var problema = result.Error.ToProblemDetails(HttpContext);
            catalogo.Simular = true;
            var plan = await _service.InyectarAsync(codigoApp, catalogo, esSistema, idApp, ct);
            if (plan.IsSuccess) problema.Extensions["plan"] = plan.Value;
            return StatusCode(StatusCodes.Status409Conflict, problema);
        }

        if (!catalogo.Simular) await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
}
