using System.Text.RegularExpressions;
using CBP.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace PassPlat.CBP.Architecture.Tests;

/// <summary>
/// S108 · D1/D2 — el middleware de CBP calla solo el aborto del cliente; otra cancelación responde 500.
/// Candado: ningún repositorio async (CBP.Data.Asynchronous y PassPlat.Datos/Repositories) convierte la
/// cancelación en error de BD.
/// </summary>
public sealed class S108CancelacionTests
{
    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static DefaultHttpContext Contexto(bool clienteAborto)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (clienteAborto)
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            ctx.RequestAborted = cts.Token;
        }
        return ctx;
    }

    private static ExceptionHandlingMiddleware Middleware() =>
        new(_ => throw new OperationCanceledException(), NullLogger<ExceptionHandlingMiddleware>.Instance);

    [Fact]
    public async Task Middleware_con_aborto_del_cliente_no_responde()
    {
        var ctx = Contexto(clienteAborto: true);

        await Middleware().InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal(0, ctx.Response.Body.Length);
    }

    [Fact]
    public async Task Middleware_con_cancelacion_del_servidor_responde_500_con_ProblemDetails()
    {
        var ctx = Contexto(clienteAborto: false);

        await Middleware().InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
        Assert.True(ctx.Response.Body.Length > 0);
    }

    [Fact]
    public void Ningun_repositorio_async_convierte_la_cancelacion_en_error_de_BD()
    {
        var raiz = RaizDelRepo();
        var carpetas = new[]
        {
            Path.GetFullPath(Path.Combine(raiz, "..", "CBP", "CBP.Data", "CBP.Data.Asynchronous")),
            Path.Combine(raiz, "PassPlat.Datos", "Repositories")
        };
        var hallazgos = new List<string>();
        foreach (var carpeta in carpetas)
        {
            Assert.True(Directory.Exists(carpeta), "No existe " + carpeta);
            foreach (var archivo in Directory.GetFiles(carpeta, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var fuente = File.ReadAllText(archivo);
                foreach (Match m in Regex.Matches(fuente, @"catch \(Exception \w+\)(?! when \(\w+ is not OperationCanceledException\))"))
                    hallazgos.Add($"{Path.GetFileName(archivo)}:{fuente[..m.Index].Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(hallazgos.Count == 0, "catch que convierte la cancelacion en error de BD: " + string.Join(", ", hallazgos));
    }
}
