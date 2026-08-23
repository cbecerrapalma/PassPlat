using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class TiposDispController : BaseApiController
{
    private readonly ITipoDispService _service;
    public TiposDispController(ITipoDispService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
}
