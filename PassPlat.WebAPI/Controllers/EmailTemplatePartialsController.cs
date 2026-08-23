using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_TEMPLATES_VER")]
public class EmailTemplatePartialsController : BaseApiController
{
    private readonly IEmailTemplatePartialService _service;
    private readonly IUnitOfWorkAsync _uow;

    public EmailTemplatePartialsController(IEmailTemplatePartialService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResultQuery(await _service.GetAllAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResultQuery(await _service.GetByIdAsync(id, ct));

    [HttpGet("nombre/{nombre}")]
    public async Task<IActionResult> GetByNombre(string nombre, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorNombreAsync(nombre, ct));

    [HttpGet("activos")]
    public async Task<IActionResult> GetActivos(CancellationToken ct)
        => FromResultQuery(await _service.ObtenerActivosAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearEmailTemplatePartialDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarEmailTemplatePartialDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { codigo = "ID_MISMATCH" });
        var result = await _service.ActualizarAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
}
