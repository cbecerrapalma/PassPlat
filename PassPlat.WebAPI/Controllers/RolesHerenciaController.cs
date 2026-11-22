using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

// S102 · D5 — herencia de roles: leer con ROLES_VER, escribir con ROLES_EDITAR, un no-sistema solo en su tenant
[Authorize(Policy = "ROLES_VER")]
public class RolesHerenciaController : BaseApiController
{
    private readonly IRolesHerenciaService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;

    public RolesHerenciaController(IRolesHerenciaService service, ITenantContext tenantContext, IUnitOfWorkAsync uow)
    {
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(int idTenant) => User.FueraDeAlcance(_tenantContext, idTenant);

    [HttpGet("tenant/{idTenant}")]
    public async Task<IActionResult> GetByTenant(int idTenant, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerPorTenantAsync(idTenant, ct));
    }

    [HttpGet("hijos/{idRolPadre}")]
    public async Task<IActionResult> GetHijos(int idRolPadre, CancellationToken ct) =>
        FromResultQuery(await _service.ObtenerHijosAsync(idRolPadre, AlcanceTenant, ct));

    [HttpGet("padres/{idRolHijo}")]
    public async Task<IActionResult> GetPadres(int idRolHijo, CancellationToken ct) =>
        FromResultQuery(await _service.ObtenerPadresAsync(idRolHijo, AlcanceTenant, ct));

    // CrearAsync guarda él mismo (necesita el Id): el controlador no repite el commit
    [HttpPost]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> Create([FromBody] CrearRolesHerenciaDto dto, CancellationToken ct)
    {
        if (FueraDeAlcance(dto.IdTenant)) return NotFound();
        return CreatedFromResult(nameof(GetByTenant), new { idTenant = dto.IdTenant }, await _service.CrearAsync(dto, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.EliminarAsync(id, AlcanceTenant, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return NoContent();
    }
}
