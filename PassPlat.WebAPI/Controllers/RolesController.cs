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
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "ROLES_VER")]
public class RolesController : BaseApiController
{
    private readonly IRolService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUsuarioPermisoService _usuarioPermiso;
    public RolesController(IRolService service, ITenantContext tenantContext, IUsuarioPermisoService usuarioPermiso)
    {
        _service = service;
        _tenantContext = tenantContext;
        _usuarioPermiso = usuarioPermiso;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? idApp)
    {
        var alcance = User.AlcanceTenant(_tenantContext);
        if (!idApp.HasValue)
        {
            if (alcance == null) return FromResultQuery(await _service.ObtenerTodosAsync(ct));
            return FromResultQuery(await _service.ObtenerParaTenantAsync(alcance.Value, ct));
        }
        var denegado = await ValidarAppAsync(idApp.Value, ct);
        if (denegado is not null) return denegado;
        return FromResultQuery(await _service.ObtenerPorAppsAsync(alcance, [idApp.Value], soloActivos: alcance != null, ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result.IsSuccess && result.Value?.IdTenant is int t && User.FueraDeAlcance(_tenantContext, t))
            return NotFound();
        return FromResultQuery(result);
    }

    [HttpGet("para-tenant/{idTenant}")]
    public async Task<IActionResult> GetParaTenant(int idTenant, CancellationToken ct, [FromQuery] int? idApp)
    {
        if (User.FueraDeAlcance(_tenantContext, idTenant)) return NotFound();
        if (idApp.HasValue)
        {
            var denegado = await ValidarAppAsync(idApp.Value, ct);
            if (denegado is not null) return denegado;
            return FromResultQuery(await _service.ObtenerPorAppsAsync(idTenant, [idApp.Value], soloActivos: true, ct));
        }
        return FromResultQuery(await _service.ObtenerParaTenantAsync(idTenant, ct));
    }

    [HttpGet("lookup/{idTenant}")]
    public async Task<IActionResult> GetLookup(int idTenant, CancellationToken ct, [FromQuery] int? idApp)
    {
        if (User.FueraDeAlcance(_tenantContext, idTenant)) return NotFound();
        if (idApp.HasValue)
        {
            var denegado = await ValidarAppAsync(idApp.Value, ct);
            if (denegado is not null) return denegado;
            return FromResultQuery(await _service.ObtenerLookupPorAppsAsync(idTenant, [idApp.Value], ct));
        }
        return FromResultQuery(await _service.ObtenerLookupPorTenantAsync(idTenant, ct));
    }

    // S111 · D3 — valida que quien llama tenga Acceso activo a la app en su tenant (403 SIN_ACCESO_APP)
    private async Task<IActionResult?> ValidarAppAsync(int idApp, CancellationToken ct)
    {
        var apps = await _usuarioPermiso.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, User.AlcanceTenant(_tenantContext), idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        return null;
    }

    [HttpPost]
    [Authorize(Policy = "ROLES_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearRolDto dto, CancellationToken ct)
    {
        // Usuario de sistema puede especificar otro tenant; otros usuarios usan el tenant del contexto
        if (!User.HasClaim("is_system", "true"))
            dto.IdTenant = _tenantContext.CurrentId;

        var idUsrEjecutor = User.IdUsuario();
        var result = await _service.CrearAsync(dto, idUsrEjecutor, ct);
        return result.IsSuccess
            ? Created(string.Empty, result.Value)
            : FromResult(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarRolDto dto, CancellationToken ct)
        => FromResultQuery(await _service.ActualizarAsync(id, dto.Nombre, dto.Descripcion, dto.Activo, User.AlcanceTenant(_tenantContext), ct));

    [HttpDelete("{id}")]
    [Authorize(Policy = "ROLES_ELIMINAR")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DesactivarAsync(id, User.AlcanceTenant(_tenantContext), ct));
}
