using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Datos;
using PassPlat.Datos.SPResults;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "PERMISOS_VER")]
[Route("api/[controller]")]
public class MatrizPermisosController : BaseApiController
{
    private readonly IUnitOfWorkAsync _uow;

    public MatrizPermisosController(IUnitOfWorkAsync uow) => _uow = uow;

    [HttpGet]
    public async Task<IActionResult> GetMatriz([FromQuery] int? idTenant, [FromQuery] int? idApp, [FromQuery] int? idRol)
    {
        var parameters = new[]
        {
            RawParameter.Int("@IdTenant", idTenant),
            RawParameter.Int("@IdApp", idApp),
            RawParameter.Int("@IdRol", idRol)
        };
        var result = await _uow.RawQuery.QuerySPAsync<MatrizPermisosResult>("SP_Matriz_Permisos_Leer", parameters);
        if (!result.IsSuccess)
            return FromResult(result);
        return Ok(result.Value);
    }
}
