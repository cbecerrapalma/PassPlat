using CBP.Data.Abstractions;
using CBP.MultiTenant.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Datos;
using PassPlat.Datos.SPResults;
using PassPlat.WebAPI.Auth;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/[controller]")]
public class PermisosEfectivosController : BaseApiController
{
    private readonly IUnitOfWorkAsync _uow;
    private readonly ITenantContext _tenantContext;

    public PermisosEfectivosController(IUnitOfWorkAsync uow, ITenantContext tenantContext)
    {
        _uow = uow;
        _tenantContext = tenantContext;
    }

    // S103 · D6 — propio o con PERMISOS_VER; un no-sistema solo ve su tenant; el sistema debe indicar el tenant
    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, [FromQuery] int? idTenant, [FromQuery] int? idApp)
    {
        var esPropio = User.EsPropio(idUsuario);
        var esSistema = User.HasClaim("is_system", "true");
        if (!esPropio && !esSistema && !User.HasClaim("permiso", "PERMISOS_VER"))
            return Forbid();

        int tenant;
        if (esSistema)
        {
            if (idTenant is null)
                return BadRequest(new { code = "VALIDACION", message = "Indique idTenant" });
            tenant = idTenant.Value;
        }
        else
        {
            tenant = idTenant ?? _tenantContext.CurrentId;
            if (tenant != _tenantContext.CurrentId)
                return NotFound();
        }

        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", tenant),
            RawParameter.Int("@IdApp", idApp)
        };
        var result = await _uow.RawQuery.QuerySPAsync<PermisosUsuarioEfectivosResult>("SP_Permisos_Usuario_Efectivos", parameters);
        if (!result.IsSuccess)
            return FromResult(result);
        return Ok(result.Value);
    }
}
