using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "ADMIN")]
[Route("api/[controller]")]
public class AudIdenExtController : BaseApiController
{
    private readonly IAudIdenExtService _service;

    public AudIdenExtController(IAudIdenExtService service) => _service = service;

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, limite, ct));

    [HttpGet("proveedor/{idProvIden}")]
    public async Task<IActionResult> ObtenerPorProveedor(int idProvIden, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorProveedorAsync(idProvIden, limite, ct));

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> ObtenerPorUsuario(int idUsuario, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, limite, ct));

    [HttpGet("metodo/{metodo}")]
    public async Task<IActionResult> ObtenerPorMetodo(string metodo, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorMetodoAsync(metodo, limite, ct));

    [HttpGet("origen/{origen}")]
    public async Task<IActionResult> ObtenerPorOrigen(string origen, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorOrigenAsync(origen, limite, ct));

    [HttpGet("resumen/{idTenant}")]
    public async Task<IActionResult> ObtenerResumen(int idTenant, [FromQuery] int limite = 20, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerResumenPorTenantAsync(idTenant, limite, ct));

    [HttpGet("errores/{idTenant}")]
    public async Task<IActionResult> ObtenerErrores(int idTenant, [FromQuery] int limite = 50, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerErroresAsync(idTenant, limite, ct));
}
