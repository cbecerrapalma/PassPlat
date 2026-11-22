using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Catalogos;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "PERMISOS_VER")]
public class PermisosController : BaseApiController
{
    private readonly IPermisoService _permisoService;
    private readonly IRolPermisoService _rolPermisoService;
    private readonly IUsuarioPermisoService _usuarioPermisoService;
    private readonly IUnitOfWorkAsync _uow;
    private readonly ITenantContext _tenantContext;
    private readonly IConcesionPermisosService _concesion;
    public PermisosController(IPermisoService permisoService, IRolPermisoService rolPermisoService, IUsuarioPermisoService usuarioPermisoService, IUnitOfWorkAsync uow, ITenantContext tenantContext, IConcesionPermisosService concesion)
    {
        _concesion = concesion;
        _permisoService = permisoService;
        _rolPermisoService = rolPermisoService;
        _usuarioPermisoService = usuarioPermisoService;
        _uow = uow;
        _tenantContext = tenantContext;
    }

    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);

    // S110 · D3 — sin idApp: sistema = todo (igual que antes); no-sistema = solo sus apps con Acceso activo.
    // Con idApp sin Acceso: SIN_ACCESO_APP (403) desde ObtenerAppsPermitidasAsync.
    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? idApp)
    {
        var apps = await _usuarioPermisoService.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, AlcanceTenant, idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        if (apps.Value is null) return FromResultQuery(await _permisoService.ObtenerTodosAsync(ct));
        return FromResultQuery(await _permisoService.ObtenerPorAppsAsync(apps.Value, false, ct));
    }
    [HttpGet("activos")] public async Task<IActionResult> GetActivos(CancellationToken ct, [FromQuery] int? idApp)
    {
        var apps = await _usuarioPermisoService.ObtenerAppsPermitidasAsync(User.IdUsuario() ?? 0, AlcanceTenant, idApp, ct);
        if (apps.IsFailure) return FromResult(apps);
        if (apps.Value is null) return FromResultQuery(await _permisoService.ObtenerActivosAsync(ct));
        return FromResultQuery(await _permisoService.ObtenerPorAppsAsync(apps.Value, true, ct));
    }
    [HttpGet("{id}")] public async Task<IActionResult> GetById(int id, CancellationToken ct) => FromResultQuery(await _permisoService.ObtenerPorIdAsync(id, ct));
    [HttpGet("rol/{idRol}")] public async Task<IActionResult> GetPermisosPorRol(int idRol, CancellationToken ct) => FromResultQuery(await _rolPermisoService.ObtenerPermisosPorRolAsync(idRol, ct));
    [HttpPost]
    [Authorize(Policy = "PERMISOS_CREAR")]
    public async Task<IActionResult> Create([FromBody] CrearPermisoDto dto, CancellationToken ct)
    {
        var result = await _permisoService.CrearAsync(dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Created(string.Empty, result.Value);
    }
    [HttpPost("rol")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> AsignarPermiso([FromBody] AsignarPermisoDto dto, CancellationToken ct)
    {
        // S113 · D3 — un no-sistema solo concede permisos que él tiene en su tenant
        if (!User.EsSistema())
        {
            var concesion = await _concesion.ValidarPermisosAsync([dto.IdPermiso], User.IdUsuario() ?? 0, _tenantContext.CurrentId, ct);
            if (concesion.IsFailure) return FromResult(concesion);
        }
        var result = await _rolPermisoService.AsignarPermisoAsync(dto, AlcanceTenant, User.IdUsuario() ?? 0, ct);
        if (result.IsFailure) return FromResultQuery(result);
        await _uow.SaveChangesAsync(ct);
        return FromResultQuery(result);
    }
    // S106 · D2 — todos los cambios de permisos de un rol en una petición, con un único commit
    [HttpPost("rol/{idRol}/aplicar")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> AplicarPermisosRol(int idRol, [FromBody] AplicarPermisosRolDto dto, CancellationToken ct)
    {
        // S113 · D3 — solo se validan los permisos que se conceden (Tiene = true); quitar no se restringe
        if (!User.EsSistema())
        {
            var concesion = await _concesion.ValidarPermisosAsync([.. (dto.Cambios ?? []).Where(c => c.Tiene).Select(c => c.IdPermiso)], User.IdUsuario() ?? 0, _tenantContext.CurrentId, ct);
            if (concesion.IsFailure) return FromResult(concesion);
        }
        var result = await _rolPermisoService.AplicarPermisosAsync(idRol, dto.Cambios ?? [], AlcanceTenant, User.IdUsuario() ?? 0, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return FromResult(result);
    }
    [HttpPut("{id}")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarPermisoDto dto, CancellationToken ct)
    {
        var result = await _permisoService.ActualizarAsync(id, dto, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
    [HttpDelete("rol/{idRol}/{idPermiso}")]
    [Authorize(Policy = "ROLES_EDITAR")]
    public async Task<IActionResult> DesasignarPermiso(int idRol, int idPermiso, CancellationToken ct)
    {
        var result = await _rolPermisoService.DesasignarPermisoAsync(idRol, idPermiso, AlcanceTenant, User.IdUsuario() ?? 0, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
    [HttpDelete("{id}")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var result = await _permisoService.EliminarAsync(id, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
