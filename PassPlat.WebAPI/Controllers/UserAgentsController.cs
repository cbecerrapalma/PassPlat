using CBP.Data.Abstractions;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;
using PassPlat.Datos;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class UserAgentsController : BaseApiController
{
    private readonly IUserAgentService _service;
    private readonly IUnitOfWorkAsync _uow;
    public UserAgentsController(IUserAgentService service, IUnitOfWorkAsync uow)
    {
        _service = service;
        _uow = uow;
    }

    [HttpGet("hash/{hashAgente}")] public async Task<IActionResult> GetByHash(string hashAgente, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorHashAsync(hashAgente));
    [HttpPost("obtener-o-crear")] public async Task<IActionResult> ObtenerOCrear([FromBody] CrearUserAgentRequest request, CancellationToken ct)
    {
        var result = await _service.ObtenerOCrearAsync(request.Agente, request.HashAgente, request.Navegador, request.Version, request.SistemaOperativo, request.EsMovil, ct);
        if (result.IsFailure) return FromResult(result);
        await _uow.SaveChangesAsync(ct);
        return Ok(result.Value);
    }
}

public class CrearUserAgentRequest
{
    public string Agente { get; init; } = string.Empty;
    public string HashAgente { get; init; } = string.Empty;
    public string? Navegador { get; init; }
    public string? Version { get; init; }
    public string? SistemaOperativo { get; init; }
    public bool? EsMovil { get; init; }
}
