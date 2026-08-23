using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class DispController : BaseApiController
{
    private readonly IDispService _service;
    private readonly IDispConfiableService _confiableService;
    private readonly IUnitOfWorkAsync _uow;
    public DispController(IDispService service, IDispConfiableService confiableService, IUnitOfWorkAsync uow)
    {
        _service = service;
        _confiableService = confiableService;
        _uow = uow;
    }

    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id));
    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosConTipoAsync(ct));
    [HttpGet("detalles/{id}")] public async Task<IActionResult> GetDetalles(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerConDetallesAsync(id));
    [HttpPost("obtener-o-crear")] public async Task<IActionResult> ObtenerOCrear([FromBody] CrearDispRequest request, CancellationToken ct)
    {
        var result = await _service.ObtenerOCrearAsync(request.IdTipoDisp, request.Fabricante, request.Modelo, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
    [HttpGet("confiables")] public async Task<IActionResult> GetAllConfiables(CancellationToken ct) => FromResultQuery(await _confiableService.ObtenerTodosConDispositivoAsync(ct));
}

public class CrearDispRequest
{
    public int IdTipoDisp { get; init; }
    public string? Fabricante { get; init; }
    public string? Modelo { get; init; }
}
