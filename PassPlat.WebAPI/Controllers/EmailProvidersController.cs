using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_PROVIDERS_VER")]
public class EmailProvidersController : BaseApiController
{
    private readonly IEmailProviderService _service;
    public EmailProvidersController(IEmailProviderService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync(ct: ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(byte id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));

    // S101 · D3 — el catálogo global de providers solo lo escribe el sistema; sin borrado físico
    [HttpPost]
    [Authorize(Policy = "SystemOnly")]
    public async Task<IActionResult> Create([FromBody] CrearEmailProviderDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedFromResult(nameof(GetById), new { id = result.Value.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SystemOnly")]
    public async Task<IActionResult> Update(byte id, [FromBody] ActualizarEmailProviderDto dto, CancellationToken ct)
        => FromResult(await _service.ActualizarAsync(id, dto, ct));
}
