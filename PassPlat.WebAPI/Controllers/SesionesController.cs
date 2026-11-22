using System.Security.Claims;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using CBP.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using CBP.Results.Errors;
using CBP.Results;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "SESIONES_VER")]
public class SesionesController : BaseApiController
{
    private readonly ISesionService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IVisibilidadUsuarioService _visibilidad;

    public SesionesController(ISesionService service, ITenantContext tenantContext, IVisibilidadUsuarioService visibilidad)
    {
        _service = service;
        _tenantContext = tenantContext;
        _visibilidad = visibilidad;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSesionDto dto, CancellationToken ct)
    {
        var idUsuarioJwt = User.IdUsuario();
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
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        return FromResultQuery(await _service.ObtenerSesionesVisiblesAsync(uid.Value, idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("activas-tenant")]
    public async Task<IActionResult> GetActivasTenant(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerSesionesActivasTenantAsync(_tenantContext.CurrentId, ct));

    [HttpPost("revocar-todas-tenant")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> RevocarTodasTenant(CancellationToken ct)
        => FromResult(await _service.RevocarTodasPorTenantAsync(_tenantContext.CurrentId, ct));

    [HttpPost("{idSesion}/revocar")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> Revocar(Guid idSesion, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        var sesion = await _service.GetByIdAsync(idSesion, ct);
        if (sesion.IsSuccess && sesion.Value is not null && sesion.Value.IdUsuario != uid)
        {
            if (sesion.Value.IdTenant != _tenantContext.CurrentId)
            {
                return FromResult(Result.Failure(CommonErrors.Authorization.Forbidden("sesión")));
            }
            var acceso = await _visibilidad.VerificarAsync(uid.Value, sesion.Value.IdUsuario, _tenantContext.CurrentId, "sesión", ct);
            if (acceso.IsFailure) return FromResult(acceso);
        }
        return FromResult(await _service.RevocarSesionAsync(idSesion, ct));
    }

    [HttpPost("revocar-todas/{idUsuario}")]
    [Authorize(Policy = "SESIONES_REVOCAR")]
    public async Task<IActionResult> RevocarTodas(int idUsuario, [FromQuery] Guid? idSesionExcluir, CancellationToken ct)
    {
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });

        var acceso = await _visibilidad.VerificarAsync(uid.Value, idUsuario, _tenantContext.CurrentId, "sesiones del usuario", ct);
        if (acceso.IsFailure) return FromResult(acceso);

        return FromResult(await _service.RevocarTodasAsync(idUsuario, _tenantContext.CurrentId, idSesionExcluir, ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResultQuery(await _service.GetByIdAsync(id, ct));
}
