using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class TiposAuditoriaController : BaseApiController
{
    private readonly ITipoAuditoriaService _service;
    public TiposAuditoriaController(ITipoAuditoriaService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
}
