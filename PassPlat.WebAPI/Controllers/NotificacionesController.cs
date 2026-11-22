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
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "NOTIFICACIONES_VER")]
public class NotificacionesController : BaseApiController
{
    private readonly INotificacionService _service;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ITenantContext _tenantContext;
    public NotificacionesController(INotificacionService service, IUnitOfWorkAsync uow, ITenantContext tenantContext)
    {
        _service = service;
        _uow = uow;
        _tenantContext = tenantContext;
    }

    // S118 · D5 (P54/P44) — notificaciones en el tenant de la sesión: el sistema y el propio usuario sin acotar;
    // cualquier otro, solo su tenant. Las listas se filtran; una notificación o un alta de un tenant ajeno → 404.
    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(NotificacionDto n) => !User.EsPropio(n.IdUsuario) && User.FueraDeAlcance(_tenantContext, n.IdTenant);

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => AlcanceTenant is int idTenant
        ? FromResultQuery(await _service.WhereAsync(n => n.IdTenant == idTenant, ct))
        : FromResultQuery(await _service.GetAllAsync(ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result.IsSuccess && FueraDeAlcance(result.Value)) return NotFound();
        return FromResultQuery(result);
    }
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        var options = new PaginationOptions<Notificacion>(request.PageNumber, request.PageSize);
        var result = AlcanceTenant is int idTenant
            ? await _service.ObtenerPaginadoPorTenantAsync(idTenant, options, ct)
            : await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("no-leidas/{idUsuario}")] public async Task<IActionResult> GetNoLeidas(int idUsuario, CancellationToken ct) => FromResultQuery(await _service.ObtenerNoLeidasAsync(idUsuario, User.AlcanceTenantDe(_tenantContext, idUsuario), ct));
    [HttpPost("{id}/marcar-leida")] public async Task<IActionResult> MarcarLeida(long id, CancellationToken ct)
    {
        var notificacion = await _service.GetByIdAsync(id, ct);
        if (notificacion.IsFailure) return FromResult(notificacion);
        if (FueraDeAlcance(notificacion.Value)) return NotFound();
        var result = await _service.MarcarComoLeidaAsync((int)id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("marcar-todas-leidas/{idUsuario}")] public async Task<IActionResult> MarcarTodasLeidas(int idUsuario, CancellationToken ct)
    {
        var result = await _service.MarcarTodasComoLeidasAsync(idUsuario, User.AlcanceTenantDe(_tenantContext, idUsuario), ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearNotificacionDto dto, CancellationToken ct)
    {
        if (User.FueraDeAlcance(_tenantContext, dto.IdTenant)) return NotFound();
        var result = await _service.CrearNotificacionAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
}
