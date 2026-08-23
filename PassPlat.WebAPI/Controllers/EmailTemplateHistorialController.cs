using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize(Policy = "EMAIL_TEMPLATES_VER")]
public class EmailTemplateHistorialController : BaseApiController
{
    private readonly IEmailTemplateHistorialService _service;
    public EmailTemplateHistorialController(IEmailTemplateHistorialService service) => _service = service;

    [HttpGet("template/{idTemplate}")] public async Task<IActionResult> GetByTemplate(int idTemplate, CancellationToken ct) => FromResultQuery(await _service.ObtenerPorTemplateAsync(idTemplate, ct));
    [HttpGet("{id}")] public async Task<IActionResult> GetById(long id, CancellationToken ct) => FromResultQuery(await _service.GetByIdAsync(id, ct));
    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => FromResultQuery(await _service.GetAllAsync(ct: ct));
}
