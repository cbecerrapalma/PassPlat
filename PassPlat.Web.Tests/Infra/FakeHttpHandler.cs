using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace PassPlat.Web.Tests.Infra;

/// <summary>Handler HTTP falso: responde por sufijo de ruta y registra las peticiones.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _respuestas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _errores = new(StringComparer.OrdinalIgnoreCase);

    // S59.1 · D2 — el panel lanza varias peticiones a la vez (Task.WhenAll): una lista
    // normal perdia registros al escribirse en paralelo y la prueba «cada recurso se pide
    // una sola vez» fallaba de forma intermitente. La cola es segura ante hilos y la
    // propiedad expone una copia estable.
    private readonly ConcurrentQueue<string> _peticiones = new();

    public IReadOnlyList<string> Peticiones => _peticiones.ToArray();

    public HttpStatusCode StatusAsignar { get; set; } = HttpStatusCode.OK;

    public string BodyAsignar { get; set; } = "{}";

    public void Responder(string sufijo, string json) => _respuestas[sufijo] = json;

    /// <summary>S59 · D9 — responde con un error (por ejemplo 400) a una ruta concreta.</summary>
    public void ResponderConError(string sufijo, HttpStatusCode status, string json) => _errores[sufijo] = (status, json);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        _peticiones.Enqueue($"{request.Method} {path}{(body is null ? "" : " " + body)}");

        if (path.EndsWith("/api/accesos/asignar", StringComparison.OrdinalIgnoreCase))
            return Json(StatusAsignar, BodyAsignar);

        foreach (var kv in _respuestas)
        {
            if (path.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                return Json(HttpStatusCode.OK, kv.Value);
        }

        foreach (var kv in _errores)
        {
            if (path.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                return Json(kv.Value.Status, kv.Value.Body);
        }

        return Json(HttpStatusCode.NotFound, "{}");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
