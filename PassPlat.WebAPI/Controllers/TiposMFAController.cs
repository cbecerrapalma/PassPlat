using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class TiposMFAController : BaseApiController
{
    private readonly ITipoMFAService _service;
    public TiposMFAController(ITipoMFAService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct) => FromResultQuery(await _service.ObtenerActivosAsync());
}
