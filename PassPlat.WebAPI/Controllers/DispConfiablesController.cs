using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Results;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Aplicacion.Services.Security;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Core;
using PassPlat.WebAPI.Auth;
namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VERDISP")]
public class DispConfiablesController : BaseApiController
{
    private readonly IDispConfiableService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IVisibilidadUsuarioService _visibilidad;
    private readonly ILogger<DispConfiablesController> _logger;
    public DispConfiablesController(IDispConfiableService service, ITenantContext tenantContext, IUnitOfWorkAsync uow, IVisibilidadUsuarioService visibilidad, ILogger<DispConfiablesController> logger)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
        _visibilidad = visibilidad;
        _logger = logger;
    }

    [HttpGet("es-confiable/{idUsuario}/{idDisp}")]
    public async Task<IActionResult> EsConfiable(int idUsuario, int idDisp, CancellationToken ct)
    {
        var denegado = await VerificarVisibilidadAsync(idUsuario, ct);
        if (denegado != null) return denegado;
        return Ok(await _service.EsConfiableAsync(idUsuario, idDisp, _tenantContext.Current?.Id, ct));
    }
    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosConDispositivoAsync(_tenantContext.Current?.Id, ct));
    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct)
    {
        var denegado = await VerificarVisibilidadAsync(idUsuario, ct);
        if (denegado != null) return denegado;
        // S96 · D5 — un fallo de esta consulta queda registrado (antes el panel mostraba la lista vacía sin rastro).
        var result = await _service.ObtenerPorUsuarioAsync(idUsuario, _tenantContext.Current?.Id, ct);
        if (result.IsFailure)
            _logger.LogError("DispConfiables usuario/{IdUsuario}: {Codigo} {Mensaje}", idUsuario, result.Error!.Code, result.Error.Message);
        return FromResultQuery(result);
    }
    [HttpPost("marcar-confiable/{idUsuario}/{idDisp}")]
    public async Task<IActionResult> MarcarConfiable(int idUsuario, int idDisp, [FromQuery] string? nombre, [FromQuery] int? idAgente, CancellationToken ct)
    {
        var denegado = await VerificarVisibilidadAsync(idUsuario, ct);
        if (denegado != null) return denegado;
        var result = await _service.MarcarComoConfiableAsync(idUsuario, _tenantContext.CurrentId, idDisp, nombre, idAgente, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("revocar-confianza/{idUsuario}/{idDisp}")]
    public async Task<IActionResult> RevocarConfianza(int idUsuario, int idDisp, CancellationToken ct)
    {
        var denegado = await VerificarVisibilidadAsync(idUsuario, ct);
        if (denegado != null) return denegado;
        var result = await _service.RevocarConfianzaAsync(idUsuario, idDisp, _tenantContext.Current?.Id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, _tenantContext.Current?.Id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("bloquear/{id}")]
    public async Task<IActionResult> Bloquear(int id, CancellationToken ct)
    {
        var result = await _service.BloquearAsync(id, _tenantContext.Current?.Id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // S75 · D3 — con tenant activo: uno mismo o miembro activo del tenant; sin tenant: sin cambios
    private async Task<IActionResult?> VerificarVisibilidadAsync(int idUsuario, CancellationToken ct)
    {
        var idTenant = _tenantContext.Current?.Id;
        if (idTenant is null) return null;
        var uid = User.IdUsuario();
        if (uid == null) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        var acceso = await _visibilidad.VerificarAsync(uid.Value, idUsuario, idTenant.Value, "dispositivos del usuario", ct);
        return acceso.IsFailure ? FromResult(acceso) : null;
    }
}
