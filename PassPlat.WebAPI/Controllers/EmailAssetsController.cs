using System.Security.Cryptography;
using System.Text;
using CBP.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassPlat.Aplicacion.Services;

namespace PassPlat.WebAPI.Controllers;

[Authorize]
public class EmailAssetsController : BaseApiController
{
    private readonly IConfigAppService _service;
    private static readonly HashSet<string> _allowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
    private const string UploadSubDir = "uploads/email";
    private const string BrandingGroup = "Branding";
    private const string LogoUrlKey = "LogoUrl";

    public EmailAssetsController(IConfigAppService service) => _service = service;

    [AllowAnonymous]
    [HttpGet("logo")]
    public async Task<IActionResult> GetLogo(CancellationToken ct)
    {
        var result = await _service.ObtenerPorGrupoAsync(BrandingGroup, ct);
        if (result.IsFailure) return FromResult(result);
        var logoUrl = result.Value.FirstOrDefault(c => c.Clave == LogoUrlKey && c.Activo)?.Valor ?? "";
        return Ok(new { logoUrl });
    }

    [HttpPost("logo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Debe seleccionar un archivo" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
            return BadRequest(new { error = "Formato no permitido. Use: png, jpg, gif, webp" });

        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadDir = Path.Combine(webRoot, UploadSubDir);
        Directory.CreateDirectory(uploadDir);

        var existing = await _service.ObtenerPorGrupoAsync(BrandingGroup, ct);
        if (existing.IsSuccess)
        {
            var currentLogo = existing.Value.FirstOrDefault(c => c.Clave == LogoUrlKey && c.Activo)?.Valor;
            if (!string.IsNullOrWhiteSpace(currentLogo) && currentLogo.StartsWith($"/{UploadSubDir}/"))
            {
                var oldPath = Path.Combine(webRoot, currentLogo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{file.FileName}:{DateTime.UtcNow.Ticks}"));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant()[..16] + ext;
        var filePath = Path.Combine(uploadDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream, ct);

        var logoUrl = $"/{UploadSubDir}/{fileName}";
        var result = await _service.SetValorAsync(BrandingGroup, LogoUrlKey, logoUrl, "string", "URL del logo para emails", ct);
        return result.IsSuccess ? Ok(new { logoUrl }) : FromResult(result);
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct)
    {
        var existing = await _service.ObtenerPorGrupoAsync(BrandingGroup, ct);
        if (existing.IsFailure) return FromResult(existing);
        var current = existing.Value.FirstOrDefault(c => c.Clave == LogoUrlKey && c.Activo);
        if (current == null) return Ok(new { logoUrl = "" });

        if (!string.IsNullOrWhiteSpace(current.Valor) && current.Valor.StartsWith($"/{UploadSubDir}/"))
        {
            var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var oldPath = Path.Combine(webRoot, current.Valor.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var disable = await _service.DesactivarAsync(current.Id, ct);
        return disable.IsSuccess ? Ok(new { logoUrl = "" }) : FromResult(disable);
    }
}
