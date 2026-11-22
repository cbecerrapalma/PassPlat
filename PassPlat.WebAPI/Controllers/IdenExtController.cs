using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Models;
using System.Security.Claims;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "FED_IDENEXT_VER")] // S114 · D1; las 6 escrituras con FED_IDENEXT_DESVINCULAR (decisión del dueño)
[Route("api/iden-ext")]
public class IdenExtController : BaseApiController
{
    private readonly IIdenExtervice _service;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ITenantContext _tenantContext;

    public IdenExtController(IIdenExtervice service, IUnitOfWorkAsync uow, ITenantContext tenantContext)
    { _service = service; _uow = uow; _tenantContext = tenantContext; }

    // S95 · D2 — un no-sistema solo lee su tenant; is_system lee todos o el tenant pedido
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(int idTenant) => User.FueraDeAlcance(_tenantContext, idTenant);

    // S98 · D1 — el actor sale del token, nunca del cliente
    private int? IdUsuarioActual => User.IdUsuario();

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> ObtenerPorUsuario(int idUsuario, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, AlcanceTenant, ct));

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    }

    [HttpGet("estado/{idEstado}")]
    public async Task<IActionResult> ObtenerPorEstado(byte idEstado, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorEstadoAsync(idEstado, AlcanceTenant, ct));

    [HttpGet("{idProvIden}/sub/{subExterno}")]
    public async Task<IActionResult> ObtenerPorSubExterno(int idProvIden, string subExterno, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorSubExternoAsync(idProvIden, subExterno, AlcanceTenant, ct));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] BuscarIdenExtRequest request, CancellationToken ct)
    {
        if (AlcanceTenant is int propio) request.IdTenant = propio;
        var result = await _service.BuscarAsync(request, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> Create([FromBody] CrearIdenExtDto dto, CancellationToken ct)
    {
        // S99 · D4 — un no-sistema solo da de alta en su tenant
        if (FueraDeAlcance(dto.IdTenant)) return NotFound();
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ObtenerPorUsuario), new { idUsuario = result.Value.IdUsuario }, result.Value);
    }

    [HttpPost("{id}/desvincular")]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> Desvincular(long id, CancellationToken ct)
    {
        if (IdUsuarioActual is not int actor) return Unauthorized();
        var result = await _service.DesvincularAsync(id, actor, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad externa desvinculada correctamente" });
    }

    [HttpPut("{id}/revocar")]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> Revocar(long id, [FromQuery] string? motivo, CancellationToken ct)
    {
        if (IdUsuarioActual is not int actor) return Unauthorized();
        var result = await _service.RevocarAsync(id, actor, motivo, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad externa revocada correctamente" });
    }

    [HttpPut("{id}/cambiar-principal")]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> CambiarPrincipal(long id, CancellationToken ct)
    {
        if (IdUsuarioActual is not int actor) return Unauthorized();
        var result = await _service.CambiarPrincipalAsync(id, actor, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad principal actualizada correctamente" });
    }

    [HttpPut("{id}/estado")]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> CambiarEstado(long id, [FromQuery] byte idEstado, CancellationToken ct)
    {
        var result = await _service.CambiarEstadoAsync(id, idEstado, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Estado actualizado correctamente" });
    }

    [HttpPost("{idUsuario}/forzar-mfa")]
    [Authorize(Policy = "FED_IDENEXT_DESVINCULAR")]
    public async Task<IActionResult> ForzarMFA(int idUsuario, CancellationToken ct)
    {
        if (IdUsuarioActual is not int actor) return Unauthorized();
        var result = await _service.ForzarMFAAsync(idUsuario, actor, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "MFA forzado correctamente. Se revocaron todos los métodos." });
    }
}
