using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_TEMPLATES_VER")]
public class EmailTemplatesController : BaseApiController
{
    private readonly IEmailTemplateService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public EmailTemplatesController(IEmailTemplateService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => User.HasClaim("is_system", "true")
            ? FromResultQuery(await _service.GetAllAsync(ct))
            : FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResultQuery(await _service.GetByIdAsync(id, ct));

    [HttpGet("nombre/{nombre}/cultura/{cultura}")]
    public async Task<IActionResult> GetByNombreCultura(string nombre, string cultura, [FromQuery] int? idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorNombreCulturaAsync(nombre, cultura, idTenant, ct));

    [HttpGet("categoria/{categoria}")]
    public async Task<IActionResult> GetByCategoria(string categoria, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorCategoriaAsync(categoria, ct));

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));

    [HttpPost]
    [Authorize(Policy = "EMAIL_TEMPLATES_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearEmailTemplateDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "EMAIL_TEMPLATES_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarEmailTemplateDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { codigo = "ID_MISMATCH" });
        var result = await _service.ActualizarAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("publicar")]
    public async Task<IActionResult> Publicar([FromBody] PublicarTemplateDto dto, CancellationToken ct)
    {
        var result = await _service.PublicarAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }

    [HttpPost("{id}/desactivar")]
    [Authorize(Policy = "EMAIL_TEMPLATES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
}
