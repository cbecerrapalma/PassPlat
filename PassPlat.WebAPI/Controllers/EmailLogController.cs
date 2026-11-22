using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

// S105 · D3 — el propio usuario ve su historial; otro usuario exige EMAIL_LOG_VER y queda acotado al tenant de la sesión (P61)
[Authorize]
public class EmailLogController : BaseApiController
{
    private readonly IEmailLogService _service;
    private readonly ITenantContext _tenantContext;

    public EmailLogController(IEmailLogService service, ITenantContext tenantContext)
    {
        _service = service;
        _tenantContext = tenantContext;
    }

    // S109 · D6 — historial general de envíos, acotado al tenant de la sesión
    [HttpGet]
    [Authorize(Policy = "EMAIL_LOG_VER")]
    public async Task<IActionResult> GetRecientes([FromQuery] string? estado, [FromQuery] int? idUsuario, [FromQuery] int top = 200, CancellationToken ct = default) =>
        FromResultQuery(await _service.ObtenerRecientesAsync(User.AlcanceTenant(_tenantContext), estado, idUsuario, top, ct));
}
