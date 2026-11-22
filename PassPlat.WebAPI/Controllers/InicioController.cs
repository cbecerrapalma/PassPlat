using System.Security.Claims;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services.Dashboard;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

/// <summary>
/// S77 · D1 — inicio por perfil. Lo usan todos los usuarios autenticados (sin politica:
/// las politicas son el claim «permiso» y son por modulo).
/// </summary>
[Authorize]
[Route("api/inicio")]
public class InicioController : BaseApiController
{
    private readonly IInicioService _service;
    private readonly ITenantContext _tenantContext;

    public InicioController(IInicioService service, ITenantContext tenantContext)
    {
        _service = service;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();
        if (idUsuario is null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var permisos = User.FindAll("permiso").Select(c => c.Value).ToHashSet();
        var idTenant = _tenantContext.Current?.Id;

        return FromResultQuery(await _service.ObtenerAsync(idUsuario.Value, idTenant, permisos, ct));
    }
}
