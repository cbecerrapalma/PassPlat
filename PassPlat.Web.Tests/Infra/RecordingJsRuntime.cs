using Microsoft.JSInterop;

namespace PassPlat.Web.Tests.Infra;

/// <summary>
/// S55.1 · K1 (D1) — doble de <see cref="IJSRuntime"/> que registra cada llamada y
/// simula los dos almacenes de la sesion (localStorage y sessionStorage).
/// </summary>
internal sealed class RecordingJsRuntime : IJSRuntime
{
    public List<(string Identificador, object?[] Argumentos)> Llamadas { get; } = [];

    public Dictionary<string, Dictionary<string, string>> Almacenes { get; } = new(StringComparer.Ordinal)
    {
        ["localStorage"] = new(StringComparer.Ordinal),
        ["sessionStorage"] = new(StringComparer.Ordinal)
    };

    public IEnumerable<string> ClavesDe(string almacen) => Almacenes[almacen].Keys;

    public int EscriturasEn(string almacen) =>
        Llamadas.Count(l => l.Identificador == $"{almacen}.setItem");

    public int BorradosEn(string almacen) =>
        Llamadas.Count(l => l.Identificador == $"{almacen}.removeItem");

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        var argumentos = args ?? [];
        Llamadas.Add((identifier, argumentos));

        if (identifier.EndsWith(".setItem", StringComparison.Ordinal) && argumentos.Length >= 2)
        {
            var almacen = almacenDe(identifier);
            Almacenes[almacen][argumentos[0]?.ToString() ?? ""] = argumentos[1]?.ToString() ?? "";
            return Valor<TValue>();
        }

        if (identifier.EndsWith(".getItem", StringComparison.Ordinal) && argumentos.Length >= 1)
        {
            var almacen = almacenDe(identifier);
            var clave = argumentos[0]?.ToString() ?? "";
            var valor = Almacenes[almacen].TryGetValue(clave, out var v) ? v : default;
            if (valor is null) return Valor<TValue>();
            return ValueTask.FromResult((TValue)Convert.ChangeType(valor, typeof(TValue)));
        }

        if (identifier.EndsWith(".removeItem", StringComparison.Ordinal) && argumentos.Length >= 1)
        {
            var almacen = almacenDe(identifier);
            Almacenes[almacen].Remove(argumentos[0]?.ToString() ?? "");
            return Valor<TValue>();
        }

        return Valor<TValue>();
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    private static string almacenDe(string identifier)
        => identifier.StartsWith("sessionStorage", StringComparison.Ordinal) ? "sessionStorage" : "localStorage";

    private static ValueTask<TValue> Valor<TValue>() => ValueTask.FromResult(default(TValue)!);
}
