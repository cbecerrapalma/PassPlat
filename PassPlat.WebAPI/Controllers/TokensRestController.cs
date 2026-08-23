using System.Security.Claims;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
[EnableRateLimiting("TokenPolicy")]
public class TokensRestController : BaseApiController
{
    private readonly ITokenRestService _service;

    public TokensRestController(ITokenRestService service) => _service = service;

    private static int? GetIdUsuario(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

[HttpPost("generar")]
    public async Task<IActionResult> Generar([FromBody] GenerarTokenRestDto dto, CancellationToken ct)
    {
        var uid = GetIdUsuario(User);
        if (uid != dto.IdUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResult(await _service.GenerarTokenAsync(
            dto.IdUsuario, dto.IdTenant, dto.IdApp, dto.HashToken,
            dto.FecVence, dto.IdDisp, dto.IdIP, dto.IdAgente, ct));
    }

    [HttpPost("validar")]
    public async Task<IActionResult> Validar([FromBody] ValidarTokenRequest request, CancellationToken ct)
    {
        var uid = GetIdUsuario(User);
        if (uid != request.IdUsuario) return Unauthorized(new { codigo = "ACCESO_DENEGADO" });
        return FromResult(await _service.ValidarTokenAsync(request.HashToken, request.IdApp, ct));
    }
}

public class ValidarTokenRequest
{
    public int    IdUsuario { get; init; }
    public string HashToken { get; init; } = string.Empty;
    public int?   IdApp     { get; init; }
}
