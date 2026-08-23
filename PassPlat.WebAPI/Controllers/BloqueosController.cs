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

[Authorize(Policy = "BLOQUEOS_VER")]
public class BloqueosController : BaseApiController
{
    private readonly IBloqueoService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public BloqueosController(IBloqueoService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet("esta-bloqueado/{idUsuario}")]
    public async Task<IActionResult> EstaBloqueado(int idUsuario, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid != idUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.EstaBloqueadoAsync(idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("activo/{idUsuario}")]
    public async Task<IActionResult> GetActivo(int idUsuario, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid != idUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerBloqueoActivoAsync(idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid != idUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResultQuery(await _service.ObtenerBloqueosPorUsuarioAsync(idUsuario, _tenantContext.CurrentId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearBloqueoDto dto, CancellationToken ct)
    {
        var uid = GetIdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        dto.IdTenant = _tenantContext.CurrentId;
        var result = await _service.CrearBloqueoAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }

    [HttpPost("desactivar-vencidos")]
    public async Task<IActionResult> DesactivarVencidos(CancellationToken ct)
    {
        var result = await _service.DesactivarBloqueosVencidosAsync(ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private int? GetIdUsuario() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
