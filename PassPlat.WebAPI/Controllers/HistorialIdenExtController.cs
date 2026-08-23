using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "USUARIOS_VER")]
public class HistorialIdenExtController : BaseApiController
{
    private readonly IHistorialIdenExtService _service;
    public HistorialIdenExtController(IHistorialIdenExtService service) => _service = service;

    [HttpGet("identidad/{idIdenExt}")]
    public async Task<IActionResult> ObtenerPorIdentidad(long idIdenExt, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorIdentidadAsync(idIdenExt, ct));

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> ObtenerPorUsuario(int idUsuario, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, ct));

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> ObtenerPorTenant(int idTenant, [FromQuery] int limit = 100, CancellationToken ct = default)
        => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, limit, ct));
}
