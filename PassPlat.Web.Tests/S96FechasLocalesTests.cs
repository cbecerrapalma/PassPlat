using System.Text.Json;
using PassPlat.Web.Helpers;

namespace PassPlat.Web.Tests.Tests.S96;

/// <summary>
/// S96 · D1 — la Web muestra la hora tal como la guarda el servidor (hora local) y la envía sin zona.
/// </summary>
public class S96FechasLocalesTests
{
    private static readonly JsonSerializerOptions Opciones = new() { Converters = { new LocalDateTimeConverter() } };

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    private static bool EsGenerado(string ruta) =>
        ruta.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin");

    [Fact]
    public void Razor_SinToLocalTime_EnPaginasYLayout()
    {
        var conToLocalTime = Directory.EnumerateFiles(Path.Combine(RaizDelRepo(), "PassPlat.Web"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !EsGenerado(f))
            .Where(f => File.ReadAllText(f).Contains(".ToLocalTime()"))
            .ToList();

        Assert.Empty(conToLocalTime);
    }

    [Fact]
    public void Conversor_EscribeHoraLocal_SinZona()
    {
        var sinTipo = new DateTime(2026, 11, 2, 22, 49, 0, DateTimeKind.Unspecified);
        var local = new DateTime(2026, 11, 2, 22, 49, 0, DateTimeKind.Local);

        Assert.Equal("\"2026-11-02T22:49:00\"", JsonSerializer.Serialize(sinTipo, Opciones));
        Assert.Equal("\"2026-11-02T22:49:00\"", JsonSerializer.Serialize(local, Opciones));
    }

    [Fact]
    public void Conversor_LeeUtc_DevuelveHoraLocal()
    {
        var desdeUtc = JsonSerializer.Deserialize<DateTime>("\"2026-11-02T22:49:00Z\"", Opciones);
        var sinZona = JsonSerializer.Deserialize<DateTime>("\"2026-11-02T22:49:00\"", Opciones);

        Assert.Equal(new DateTime(2026, 11, 2, 22, 49, 0, DateTimeKind.Utc).ToLocalTime(), desdeUtc);
        Assert.Equal(new DateTime(2026, 11, 2, 22, 49, 0), sinZona);
    }
}
