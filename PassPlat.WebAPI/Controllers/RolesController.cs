using System.Security.Claims;
using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using CBP.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "ROLES_VER")]
public class RolesController : BaseApiController
{
    private readonly IRolService _service;
    private readonly ITenantContext _tenantContext;
    public RolesController(IRolService service, ITenantContext tenantContext)
    {
        _service = service;
        _tenantContext = tenantContext;
    }

    private int? GetIdUsuarioActual()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
    }

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.ObtenerTodosAsync(ct));

    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));

    [HttpGet("para-tenant/{idTenant}")]
    public async Task<IActionResult> GetParaTenant(int idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerParaTenantAsync(idTenant, ct));

    [HttpGet("lookup/{idTenant}")]
    public async Task<IActionResult> GetLookup(int idTenant, CancellationToken ct)
        => FromResultQuery(await _service.ObtenerLookupPorTenantAsync(idTenant, ct));

    [HttpPost]
    [Authorize(Policy = "ROLES_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearRolDto dto, CancellationToken ct)
    {
        // Usuario de sistema puede especificar otro tenant; otros usuarios usan el tenant del contexto
        if (!User.HasClaim("is_system", "true"))
            dto.IdTenant = _tenantContext.CurrentId;

        var idUsrEjecutor = GetIdUsuarioActual();
        var result = await _service.CrearAsync(dto, idUsrEjecutor, ct);
        return result.IsSuccess
            ? Created(string.Empty, result.Value)
            : FromResult(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarRolDto dto, CancellationToken ct)
    {
        var idUsrEjecutor = GetIdUsuarioActual();
        return FromResultQuery(await _service.ActualizarAsync(id, dto.Nombre, dto.Descripcion, dto.Activo, idUsrEjecutor, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ROLES_ELIMINAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var idUsrEjecutor = GetIdUsuarioActual();
        return FromResult(await _service.DesactivarAsync(id, idUsrEjecutor, ct));
    }
}
