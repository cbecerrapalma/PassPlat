using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Datos;
using PassPlat.Datos.SPResults;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[Route("api/[controller]")]
public class PermisosEfectivosController : BaseApiController
{
    private readonly IUnitOfWorkAsync _uow;

    public PermisosEfectivosController(IUnitOfWorkAsync uow) => _uow = uow;

    [HttpGet("usuario/{idUsuario}")]
    public async Task<IActionResult> GetByUsuario(int idUsuario, [FromQuery] int? idTenant, [FromQuery] int? idApp)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdUsuario", idUsuario),
            RawParameter.Int("@IdTenant", idTenant ?? 0),
            RawParameter.Int("@IdApp", idApp)
        };
        var result = await _uow.RawQuery.QuerySPAsync<PermisosUsuarioEfectivosResult>("SP_Permisos_Usuario_Efectivos", parameters);
        if (!result.IsSuccess)
            return FromResult(result);
        return Ok(result.Value);
    }
}
