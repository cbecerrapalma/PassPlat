using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "NOTIFICACIONES_VER")]
public class NotificacionesController : BaseApiController
{
    private readonly INotificacionService _service;
    private readonly IUnitOfWorkAsync _uow;
    public NotificacionesController(INotificacionService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id));
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<Notificacion>(request.PageNumber, request.PageSize);
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("no-leidas/{idUsuario}")] public async Task<IActionResult> GetNoLeidas(int idUsuario, CancellationToken ct) => FromResultQuery(await _service.ObtenerNoLeidasAsync(idUsuario));
    [HttpGet("contar-no-leidas/{idUsuario}")] public async Task<IActionResult> ContarNoLeidas(int idUsuario, CancellationToken ct) => Ok(await _service.ContarNoLeidasAsync(idUsuario));
    [HttpPost("{id}/marcar-leida")] public async Task<IActionResult> MarcarLeida(long id, CancellationToken ct)
    {
        var result = await _service.MarcarComoLeidaAsync((int)id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("marcar-todas-leidas/{idUsuario}")] public async Task<IActionResult> MarcarTodasLeidas(int idUsuario, CancellationToken ct)
    {
        var result = await _service.MarcarTodasComoLeidasAsync(idUsuario, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearNotificacionDto dto, CancellationToken ct)
    {
        var result = await _service.CrearNotificacionAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
}
