using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class IPsController : BaseApiController
{
    private readonly IIPService _service;
    private readonly IUnitOfWorkAsync _uow;
    public IPsController(IIPService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("direccion/{direccion}")] public async Task<IActionResult> GetByDireccion(string direccion, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorDireccionAsync(direccion));
    [HttpPost("obtener-o-crear")] public async Task<IActionResult> ObtenerOCrear([FromBody] CrearIPRequest request, CancellationToken ct)
    {
        var result = await _service.ObtenerOCrearAsync(request.Direccion, request.TipoIP, request.Pais, request.Ciudad, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
    [HttpPost("{id}/marcar-sospechosa")] public async Task<IActionResult> MarcarSospechosa(int id, CancellationToken ct)
    {
        var result = await _service.MarcarComoSospechosaAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}

public class CrearIPRequest
{
    public string Direccion { get; init; } = string.Empty;
    public byte TipoIP { get; init; }
    public string? Pais { get; init; }
    public string? Ciudad { get; init; }
}
