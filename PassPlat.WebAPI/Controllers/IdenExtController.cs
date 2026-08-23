using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Datos.Models;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/iden-ext")]
public class IdenExtController : BaseApiController
{
    private readonly IIdenExtervice _service;
    private readonly IUnitOfWorkAsync _uow;

    public IdenExtController(IIdenExtervice service, IUnitOfWorkAsync uow)
    { _service = service; _uow = uow; }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> ObtenerPorUsuario(int idUsuario, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, ct));

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));

    [HttpGet("estado/{idEstado}")]
    public async Task<IActionResult> ObtenerPorEstado(byte idEstado, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorEstadoAsync(idEstado, ct));

    [HttpGet("{idProvIden}/sub/{subExterno}")]
    public async Task<IActionResult> ObtenerPorSubExterno(int idProvIden, string subExterno, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorSubExternoAsync(idProvIden, subExterno, ct));

    [HttpGet("page")]
    public async Task<IActionResult> GetPaged([FromQuery] BuscarIdenExtRequest request, CancellationToken ct)
    {
        var result = await _service.BuscarAsync(request, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearIdenExtDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ObtenerPorUsuario), new { idUsuario = result.Value.IdUsuario }, result.Value);
    }

    [HttpPost("{id}/desvincular/{idUsuarioElimina}")]
    public async Task<IActionResult> Desvincular(long id, int idUsuarioElimina, CancellationToken ct)
    {
        var result = await _service.DesvincularAsync(id, idUsuarioElimina, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad externa desvinculada correctamente" });
    }

    [HttpPut("{id}/revocar")]
    public async Task<IActionResult> Revocar(long id, [FromQuery] int idUsuarioRevoca, [FromQuery] string? motivo, CancellationToken ct)
    {
        var result = await _service.RevocarAsync(id, idUsuarioRevoca, motivo, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad externa revocada correctamente" });
    }

    [HttpPut("{id}/cambiar-principal")]
    public async Task<IActionResult> CambiarPrincipal(long id, [FromQuery] int idUsuario, CancellationToken ct)
    {
        var result = await _service.CambiarPrincipalAsync(id, idUsuario, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Identidad principal actualizada correctamente" });
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(long id, [FromQuery] byte idEstado, CancellationToken ct)
    {
        var result = await _service.CambiarEstadoAsync(id, idEstado, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "Estado actualizado correctamente" });
    }

    [HttpPost("{idUsuario}/forzar-mfa")]
    public async Task<IActionResult> ForzarMFA(int idUsuario, [FromQuery] int idUsuarioAdmin, CancellationToken ct)
    {
        var result = await _service.ForzarMFAAsync(idUsuario, idUsuarioAdmin, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(new { mensaje = "MFA forzado correctamente. Se revocaron todos los métodos." });
    }
}
