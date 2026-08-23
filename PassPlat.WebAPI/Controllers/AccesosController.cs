using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

public class AccesosController : BaseApiController
{
    private readonly IAccesoService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public AccesosController(IAccesoService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("tiene-acceso/{idUsuario}/{idApp}")]
    [Authorize(Policy = "ACCESOS_VER")]
    public async Task<IActionResult> TieneAcceso(int idUsuario, int idApp, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid != idUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.TieneAccesoAsync(idUsuario, idApp, ct));
    }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        var isSystem = User.HasClaim("is_system", "true");
        var hasUsuariosVer = User.HasClaim("permiso", "USUARIOS_VER");
        if (uid != idUsuario && !isSystem && !hasUsuariosVer)
            return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerAccesosUsuarioAsync(idUsuario, ct));
    }

    [HttpGet("tenant-app/{idApp}")]
    [Authorize(Policy = "ACCESOS_VER")]
    public async Task<IActionResult> GetByTenantYApp(int idApp, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerAccesosPorTenantYAppAsync(_tenantContext.CurrentId, idApp, ct));

    [HttpGet("rol/{idRol}")]
    [Authorize(Policy = "ACCESOS_VER")]
    public async Task<IActionResult> GetByRol(int idRol, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerAccesosPorRolAsync(idRol, ct));

    [HttpPost("asignar")]
    [Authorize(Policy = "ACCESOS_ASIGNAR")]
    public async Task<IActionResult> Asignar([FromBody] AsignarAccesoDto dto, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.AsignarAccesoAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }

    [HttpPost("revocar/{idUsuario}/{idApp}")]
    [Authorize(Policy = "ACCESOS_REVOCAR")]
    public async Task<IActionResult> Revocar(int idUsuario, int idApp, CancellationToken ct)
    {
        var result = await _service.RevocarAccesoAsync(idUsuario, idApp, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
