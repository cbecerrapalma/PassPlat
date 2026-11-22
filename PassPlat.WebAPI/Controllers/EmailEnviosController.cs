using CBP.WebApi.Controllers;
using CBP.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PassPlat.Aplicacion.Dtos.Core;
using PassPlat.Aplicacion.Services.Email;
using PassPlat.Aplicacion.Validations.Core;

namespace PassPlat.WebAPI.Controllers;

// S78: endpoint de envío de email por plantilla para aplicaciones externas
// POST /api/email/enviar-plantilla
// Requiere: EMAIL_API_ENVIAR policy, rate limit EmailApiPolicy (sliding 1min/30)
[Authorize(Policy = "EMAIL_API_ENVIAR")]
[EnableRateLimiting("EmailApiPolicy")]
[Route("api/email")]
public sealed class EmailEnviosController : BaseApiController
{
    private readonly IEmailApiService _emailApiService;
    private readonly EnviarEmailPlantillaValidator _validator;

    public EmailEnviosController(IEmailApiService emailApiService, EnviarEmailPlantillaValidator validator)
    {
        _emailApiService = emailApiService ?? throw new ArgumentNullException(nameof(emailApiService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    [HttpPost("enviar-plantilla")]
    public async Task<IActionResult> EnviarPlantilla([FromBody] EnviarEmailPlantillaRequest request, CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return BadRequest(new
            {
                codigo = "VALIDATION_ERROR",
                mensaje = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))
            });

        var result = await _emailApiService.EnviarAsync(request, ct);
        if (result.IsFailure)
        {
            var problem = result.Error!.ToProblemDetails(HttpContext);
            return StatusCode(problem.Status ?? 400, problem);
        }

        return Accepted(result.Value);
    }
}