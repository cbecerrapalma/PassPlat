using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_ACCOUNTS_VER")]
public class EmailAccountsController : BaseApiController
{
    private readonly IEmailAccountService _service;
    public EmailAccountsController(IEmailAccountService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync(ct: ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct) => FromResultQuery(await _service.ObtenerActivosAsync(ct));
    [HttpGet("predeterminada")] public async Task<IActionResult> GetPredeterminada(CancellationToken ct) => FromResultQuery(await _service.ObtenerPredeterminadaAsync(ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearEmailAccountDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")] public async Task<IActionResult> Update(int id, [FromBody] ActualizarEmailAccountDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        return Ok(result.Value);
    }
}
