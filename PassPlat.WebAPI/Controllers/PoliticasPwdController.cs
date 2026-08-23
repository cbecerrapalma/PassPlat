using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "POLITICASPWD_VER")]
public class PoliticasPwdController : BaseApiController
{
    private readonly IPoliticaPwdService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public PoliticasPwdController(IPoliticaPwdService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var isSystem = User.HasClaim("is_system", "true");
        if (isSystem)
            return FromResultQuery(await _service.GetAllAsync());
        return FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct));
    }
    [HttpGet("tenant")] public async Task<IActionResult> GetByTenant(CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id));
    [HttpGet("aplicable/{idApp?}")] public async Task<IActionResult> GetAplicable(int? idApp, CancellationToken ct) => FromResultQuery(await _service.ObtenerPoliticaAplicableAsync(_tenantContext.CurrentId, idApp));
    [HttpGet("global")] public async Task<IActionResult> GetGlobal(CancellationToken ct) => FromResultQuery(await _service.ObtenerPoliticaGlobalAsync());
    [HttpGet("rol/{idRol}")] public async Task<IActionResult> GetParaRol(int idRol, CancellationToken ct) => FromResultQuery(await _service.ObtenerPoliticaParaRolAsync(_tenantContext.CurrentId, idRol));
    [HttpPost]
    [Authorize(Policy = "POLITICAS_PWD_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearPoliticaPwdDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
    [HttpPut("{id}")]
    [Authorize(Policy = "POLITICAS_PWD_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarPoliticaPwdDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpPost("{id}/desactivar")] public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarPoliticaAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
