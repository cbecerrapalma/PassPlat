using CBP.Results.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PassPlat.Aplicacion.Errors;

namespace PassPlat.WebAPI.Filters;

/// <summary>
/// S64: un <c>Result.Failure(código, mensaje)</c> sin tipo sale de <c>FromResult</c> como
/// ProblemDetails 500. Si el código es de negocio (<see cref="ClasificacionErrores"/>), este
/// filtro corrige estado, título y tipo; el resto del cuerpo (detail, errorCode, traceId,
/// errors) no cambia. Los códigos técnicos, los errores ya tipados y las excepciones no se tocan.
/// </summary>
public sealed class ErrorCodigoHttpFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails problem } resultado)
            return;
        if ((problem.Status ?? resultado.StatusCode) != StatusCodes.Status500InternalServerError)
            return;
        if (!problem.Extensions.TryGetValue("errorCode", out var valor) || valor is not string codigo)
            return;

        var tipo = ClasificacionErrores.TipoPorCodigo(codigo);
        if (tipo is null)
            return;

        var (status, title, type) = Describir(tipo.Value);
        problem.Status = status;
        problem.Title = title;
        problem.Type = type;
        resultado.StatusCode = status;
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    // Mismos valores que CBP.WebApi ErrorExtensions para cada ErrorType.
    private static (int Status, string Title, string Type) Describir(ErrorType tipo) => tipo switch
    {
        ErrorType.Validation => (400, "Validation Error", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
        ErrorType.BadRequest => (400, "Bad Request", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
        ErrorType.Unauthorized => (401, "Unauthorized", "https://tools.ietf.org/html/rfc7235#section-3.1"),
        ErrorType.Forbidden => (403, "Forbidden", "https://tools.ietf.org/html/rfc7231#section-6.5.3"),
        ErrorType.NotFound => (404, "Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
        ErrorType.Conflict => (409, "Conflict", "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
        _ => (500, "Internal Server Error", "https://tools.ietf.org/html/rfc7231#section-6.6.1"),
    };
}
