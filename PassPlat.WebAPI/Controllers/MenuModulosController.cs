using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

/// <summary>
/// S83 · D1 — menú de la app del token: módulos activos de su <c>IdApp</c> filtrados por los permisos <c>_VER</c>
/// del usuario. Sin parámetro de app: nadie pide el menú de otra app.
/// </summary>
[Authorize]
[Route("api/modulos/menu")]
public class MenuModulosController : BaseApiController
{
    private readonly IModuloService _service;

    public MenuModulosController(IModuloService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("IdApp")?.Value, out var idApp))
            return Unauthorized();

        var permisos = User.FindAll("permiso").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        var esSistema = User.HasClaim("is_system", "true");
        return FromResultQuery(await _service.ObtenerMenuAsync(idApp, permisos, esSistema, ct));
    }
}
