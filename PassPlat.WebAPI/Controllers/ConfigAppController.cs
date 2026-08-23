using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.Services.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "CONFIG_APP_VER")]
public class ConfigAppController : BaseApiController
{
    private readonly IConfigAppService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    public ConfigAppController(IConfigAppService service, ITenantContext tenantContext, IUnitOfWorkAsync uow) { _service = service; _tenantContext = tenantContext; _uow = uow; }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct)
        => User.HasClaim("is_system", "true")
            ? FromResultQuery(await _service.ObtenerTodasAsync(ct))
            : FromResultQuery(await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorIdAsync(id, ct));
    [HttpGet("page")] public async Task<IActionResult> GetPaged([FromQuery] PaginationRequest request, [FromQuery] string? search, CancellationToken ct)
    {
        var options = new PaginationOptions<ConfigApp>(request.PageNumber, request.PageSize);
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (!User.HasClaim("is_system", "true"))
            {
                var allResult = await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct);
                if (allResult.IsFailure) return FromResult(allResult);
                var filtered = allResult.Value.Where(c => c.Clave.Contains(search, StringComparison.OrdinalIgnoreCase) || (c.Valor ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) || c.Grupo.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                return Ok(ToPagedResponse(new PagedResultDto<ConfigAppDto>(filtered, filtered.Count, request.PageNumber, request.PageSize)));
            }
            return FromResultQuery(await _service.ObtenerPaginadoConBusquedaAsync(options, search, ct));
        }
        if (!User.HasClaim("is_system", "true"))
        {
            var tenantResult = await _service.ObtenerPorTenantAsync(_tenantContext.CurrentId, ct);
            if (tenantResult.IsFailure) return FromResult(tenantResult);
            return Ok(ToPagedResponse(new PagedResultDto<ConfigAppDto>(tenantResult.Value, tenantResult.Value.Count, request.PageNumber, request.PageSize)));
        }
        var result = await _service.GetPagedAsync(options, ct);
        return result.IsSuccess ? Ok(ToPagedResponse(result.Value)) : FromResult(result);
    }
    [HttpGet("grupo/{grupo}")] public async Task<IActionResult> GetByGrupo(string grupo, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorGrupoAsync(grupo, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CrearConfigAppDto dto, CancellationToken ct)
    {
        var result = await _service.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Created(string.Empty, result.Value);
    }
    [HttpPut("{id}")]
    [Authorize(Policy = "CONFIG_APP_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarConfigAppDto dto, CancellationToken ct)
    {
        var result = await _service.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResultQuery(result);
        await _uow.SaveChangesAsync(ct);
        return FromResultQuery(result);
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DesactivarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
