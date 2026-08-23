using CBP.Security.Cryptography.Models;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Route("api/password/seguridad")]
[Authorize]
public class PasswordSecurityController : BaseApiController
{
    private readonly IPassPlatPasswordSecurity _service;
    private readonly IPoliticaPwdService _politicaService;

    public PasswordSecurityController(IPassPlatPasswordSecurity service, IPoliticaPwdService politicaService)
    {
        _service = service;
        _politicaService = politicaService;
    }

    [HttpPost("validar")]
    public async Task<IActionResult> Validar([FromBody] ValidarPasswordRequest request, CancellationToken ct)
    {
        if (request.PoliticaId.HasValue)
        {
            var politica = await _politicaService.GetByIdAsync(request.PoliticaId.Value, ct);
            if (politica.IsFailure || politica.Value == null)
                return BadRequest(new { codigo = "POLITICA_NOT_FOUND" });

            var policy = MapToPoliticaPwd(politica.Value);
            var result = await _service.ValidatePasswordAsync(request.Password, policy, null, ct);
            return Ok(result);
        }

        var global = await _politicaService.ObtenerPoliticaGlobalAsync(ct);
        if (global.IsFailure || global.Value == null)
            return BadRequest(new { codigo = "NO_GLOBAL_POLICY" });

        var globalPolicy = MapToPoliticaPwd(global.Value);
        var validationResult = await _service.ValidatePasswordAsync(request.Password, globalPolicy, null, ct);
        return Ok(validationResult);
    }

    [HttpPost("analizar")]
    public async Task<IActionResult> Analizar([FromBody] AnalizarPasswordRequest request, CancellationToken ct)
    {
        var result = await _service.AnalyzePasswordAsync(request.Password, null, ct);
        return Ok(result);
    }

    [HttpPost("generar")]
    public async Task<IActionResult> Generar([FromBody] GenerarPasswordRequest request, CancellationToken ct)
    {
        if (request.PoliticaId.HasValue)
        {
            var politica = await _politicaService.GetByIdAsync(request.PoliticaId.Value, ct);
            if (politica.IsFailure || politica.Value == null)
                return BadRequest(new { codigo = "POLITICA_NOT_FOUND" });

            var policy = MapToPoliticaPwd(politica.Value);
            var result = await _service.GenerateTemporaryPasswordAsync(policy, ct);
            return Ok(result);
        }

        var global = await _politicaService.ObtenerPoliticaGlobalAsync(ct);
        if (global.IsFailure || global.Value == null)
            return BadRequest(new { codigo = "NO_GLOBAL_POLICY" });

        var globalPolicy = MapToPoliticaPwd(global.Value);
        var genResult = await _service.GenerateTemporaryPasswordAsync(globalPolicy, ct);
        return Ok(genResult);
    }

    private static PoliticaPwd MapToPoliticaPwd(PassPlat.Aplicacion.Dtos.Core.PoliticaPwdDto dto) => new()
    {
        Id = dto.Id,
        Version = dto.Version,
        Codigo = dto.Codigo,
        Nombre = dto.Nombre,
        LongMin = dto.LongMin,
        LongMax = dto.LongMax,
        ReqMayuscula = dto.ReqMayuscula,
        ReqMinuscula = dto.ReqMinuscula,
        ReqNumero = dto.ReqNumero,
        ReqEspecial = dto.ReqEspecial,
        CaracteresEspeciales = dto.CaracteresEspeciales,
        ProhSecuenciales = dto.ProhSecuenciales,
        ProhRepetitivos = dto.ProhRepetitivos,
        ProhPatrones = dto.ProhPatrones,
        ProhPwdComun = dto.ProhPwdComun,
        ProhInfoUsuario = dto.ProhInfoUsuario,
        VerificarBrechas = dto.VerificarBrechas,
        PermitirEspacios = dto.PermitirEspacios,
        DiasVigencia = (short)dto.DiasVigencia,
        PwdRecordadas = dto.PwdRecordadas,
        MaxIntentos = dto.MaxIntentos,
        DurBloqueoMin = dto.DurBloqueoMin,
        Activa = dto.Activa,
        FecCrea = dto.FecCrea,
        FecMod = dto.FecMod
    };
}

public class ValidarPasswordRequest
{
    public string Password { get; init; } = string.Empty;
    public int? PoliticaId { get; init; }
}

public class AnalizarPasswordRequest
{
    public string Password { get; init; } = string.Empty;
}

public class GenerarPasswordRequest
{
    public int? PoliticaId { get; init; }
}
