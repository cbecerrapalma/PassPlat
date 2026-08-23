using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "MODULOS_VER")]
public class TiposModuloController : BaseApiController
{
    private readonly ITipoModuloService _service;
    public TiposModuloController(ITipoModuloService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync(ct: ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));
    [HttpGet("codigo/{codigo}")] public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorCodigoAsync(codigo, ct));
}
