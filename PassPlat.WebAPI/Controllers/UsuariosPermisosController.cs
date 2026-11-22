using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using PassPlat.WebAPI.Auth;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

// S102 · D5 — permisos directos: leer con PERMISOS_VER, escribir con PERMISOS_EDITAR, un no-sistema solo en su tenant
[Authorize(Policy = "PERMISOS_VER")]
public class UsuariosPermisosController : BaseApiController
{
    private readonly IUsuarioPermisoService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWorkAsync _uow;
    private readonly IConcesionPermisosService _concesion;

    public UsuariosPermisosController(IUsuarioPermisoService service, ITenantContext tenantContext, IUnitOfWorkAsync uow, IConcesionPermisosService concesion)
    {
        _concesion = concesion;
        _service = service;
        _tenantContext = tenantContext;
        _uow = uow;
    }

    private int? AlcanceTenant => User.AlcanceTenant(_tenantContext);
    private bool FueraDeAlcance(int idTenant) => User.FueraDeAlcance(_tenantContext, idTenant);

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, CancellationToken ct) =>
        FromResultQuery(await _service.ObtenerPorUsuarioAsync(idUsuario, AlcanceTenant, ct));

    // S103 · D1 — apps del usuario con Acceso activo (y membresía) dentro del alcance
    [HttpGet("contextos/{idUsuario}")]
    public async Task<IActionResult> GetContextos(int idUsuario, CancellationToken ct) =>
        FromResultQuery(await _service.ObtenerContextosAsync(idUsuario, AlcanceTenant, ct));

    // S103 · D3 — todos los permisos de la app con su origen y si el usuario los tiene
    [HttpGet("editor/{idUsuario}")]
    public async Task<IActionResult> GetEditor(int idUsuario, [FromQuery] int idTenant, [FromQuery] int idApp, CancellationToken ct)
    {
        if (FueraDeAlcance(idTenant)) return NotFound();
        return FromResultQuery(await _service.ObtenerEditorAsync(idUsuario, idTenant, idApp, ct));
    }

    // S103 · D4 — aplica todos los cambios en una petición (un único commit)
    [HttpPost("aplicar")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Aplicar([FromBody] AplicarPermisosUsuarioDto dto, CancellationToken ct)
    {
        if (FueraDeAlcance(dto.IdTenant)) return NotFound();
        // S113 · D3 — un no-sistema solo concede permisos directos que él tiene en su tenant
        if (!User.EsSistema())
        {
            var concesion = await _concesion.ValidarPermisosAsync([.. dto.Cambios.Where(c => c.Tiene).Select(c => c.IdPermiso)], User.IdUsuario() ?? 0, dto.IdTenant, ct);
            if (concesion.IsFailure) return FromResult(concesion);
        }
        var result = await _service.AplicarPermisosAsync(dto, User.IdUsuario() ?? 0, User.EsSistema(), ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return FromResult(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "PERMISOS_EDITAR")]
    public async Task<IActionResult> Revocar(int id, CancellationToken ct)
    {
        var result = await _service.RevocarPermisoAsync(id, AlcanceTenant, User.IdUsuario() ?? 0, ct);
        if (result.IsFailure) return FromResult(result);

        await _uow.SaveChangesAsync(ct);

        return NoContent();
    }
}
