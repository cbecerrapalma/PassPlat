using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "ESTADOS_USR_VER")]
public class EstadosUsrController : BaseApiController
{
    private readonly IEstadoUsrService _service;
    public EstadosUsrController(IEstadoUsrService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync());
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct) => FromResultQuery(await _service.ObtenerActivosAsync());
    [HttpGet("codigo/{codigo}")] public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorCodigoAsync(codigo));
}
