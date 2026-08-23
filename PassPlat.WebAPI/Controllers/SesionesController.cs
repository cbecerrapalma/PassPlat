using System.Security.Claims;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using CBP.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "SESIONES_VER")]
public class SesionesController : BaseApiController
{
    private readonly ISesionService _service;
    private readonly ITenantContext _tenantContext;

    public SesionesController(ISesionService service, ITenantContext tenantContext)
    {
        _service = service;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSesionDto dto, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != dto.IdUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.CrearSesionAsync(
            dto.IdUsuario, dto.IdTenant, dto.IdApp, dto.IdTokenExt,
            dto.FecExpira, dto.HashRefresh, dto.IdDisp, dto.IdIP,
            dto.IdSesionPadre, ct);

        if (result.IsFailure)
        {
            var problem = result.Error!.ToProblemDetails(HttpContext);
            return StatusCode(problem.Status ?? 400, problem);
        }

        return CreatedFromResult(nameof(GetById), new { id = result.Value.IdSesion }, result);
    }

    [HttpGet("activas/{idUsuario}")]
    public async Task<IActionResult> GetActivas(int idUsuario, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ObtenerSesionesActivasAsync(idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("contar/{idUsuario}")]
    public async Task<IActionResult> Contar(int idUsuario, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ContarSesionesActivasAsync(idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("contar-tenant")]
    public async Task<IActionResult> ContarPorTenant(CancellationToken ct)
        => FromResultQuery(await _service.ContarSesionesActivasPorTenantAsync(_tenantContext.CurrentId, ct));

    [HttpGet("activas-tenant")]
    public async Task<IActionResult> GetActivasTenant(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerSesionesActivasTenantAsync(_tenantContext.CurrentId, ct));

    [HttpPost("revocar-todas-tenant")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> RevocarTodasTenant(CancellationToken ct)
        => FromResult(await _service.RevocarTodasPorTenantAsync(_tenantContext.CurrentId, ct));

    [HttpGet("token-ext/{idTokenExt}")]
    public async Task<IActionResult> GetByTokenExt(string idTokenExt, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorIdTokenExtAsync(idTokenExt, ct));

    [HttpPost("{idSesion}/revocar")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> Revocar(Guid idSesion, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        var sesion = await _service.GetByIdAsync(idSesion, ct);
        if (sesion.IsSuccess && sesion.Value?.IdUsuario != uid)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResult(await _service.RevocarSesionAsync(idSesion, ct));
    }

    [HttpPost("revocar-todas/{idUsuario}")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> RevocarTodas(int idUsuario, [FromQuery] Guid? idSesionExcluir, CancellationToken ct)
    {
        var idUsuarioJwt = GetIdUsuario();
        if (idUsuarioJwt != idUsuario)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResult(await _service.RevocarTodasAsync(idUsuario, _tenantContext.CurrentId, idSesionExcluir, ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResultQuery(await _service.GetByIdAsync(id, ct));

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;
}
