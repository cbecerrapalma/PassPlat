using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "ESTADOS_USR_VER")]
public class EstIdenExtController : BaseApiController
{
    private readonly IEstIdenExtService _service;
    public EstIdenExtController(IEstIdenExtService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync(ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(byte id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id, ct));
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct) => FromResultQuery(await _service.ObtenerActivosAsync(ct));
    [HttpGet("nombre/{nombre}")] public async Task<IActionResult> GetByNombre(string nombre, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorNombreAsync(nombre, ct));
}
