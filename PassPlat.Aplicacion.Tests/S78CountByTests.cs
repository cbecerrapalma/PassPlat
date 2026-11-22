using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace PassPlat.Aplicacion.Tests;

/// <summary>
/// S78 — extensiones de CBP.Data.Asynchronous: conteo agrupado (CountByAsync) y primeras N filas (TopAsync)
/// sobre EF InMemory, sobre un IQueryable en memoria y con un error de consulta.
/// </summary>
public class S78CountByTests
{
    public class Item
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    private sealed class ContextoPrueba(DbContextOptions<ContextoPrueba> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    private static readonly Item[] _items =
    [
        new() { Id = 1, Clave = "A", Orden = 3 },
        new() { Id = 2, Clave = "A", Orden = 1 },
        new() { Id = 3, Clave = "B", Orden = 2 }
    ];

    private static async Task<RepositoryAsync<Item>> CrearRepositorioAsync()
    {
        var ctx = new ContextoPrueba(new DbContextOptionsBuilder<ContextoPrueba>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        ctx.Items.AddRange(_items.Select(i => new Item { Id = i.Id, Clave = i.Clave, Orden = i.Orden }));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new RepositoryAsync<Item>(ctx);
    }

    [Fact]
    public async Task CountByAsync_InMemoryEf_agrupa_y_cuenta_por_clave()
    {
        var repo = await CrearRepositorioAsync();

        var result = await repo.CountByAsync(i => i.Clave, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new KeyValuePair<string, int>("A", 2), new KeyValuePair<string, int>("B", 1)],
            result.Value.OrderBy(kv => kv.Key).ToList());
    }

    [Fact]
    public async Task CountByAsync_con_predicado_y_top_devuelve_los_mayores()
    {
        var repo = await CrearRepositorioAsync();

        var top1 = await repo.CountByAsync(i => i.Clave, top: 1, ct: TestContext.Current.CancellationToken);
        var soloB = await repo.CountByAsync(i => i.Clave, i => i.Clave == "B", ct: TestContext.Current.CancellationToken);

        Assert.Equal([new KeyValuePair<string, int>("A", 2)], top1.Value);
        Assert.Equal([new KeyValuePair<string, int>("B", 1)], soloB.Value);
    }

    [Fact]
    public async Task CountByAsync_IQueryable_en_memoria_no_falla()
    {
        var repo = new Mock<IReadRepositoryAsync<Item>>();
        repo.Setup(r => r.Query()).Returns(_items.ToList().AsQueryable());

        var result = await repo.Object.CountByAsync(i => i.Clave, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new KeyValuePair<string, int>("A", 2), new KeyValuePair<string, int>("B", 1)],
            result.Value.OrderBy(kv => kv.Key).ToList());
    }

    [Fact]
    public async Task CountByAsync_error_de_consulta_devuelve_DATABASE_ERROR()
    {
        var repo = new Mock<IReadRepositoryAsync<Item>>();
        repo.Setup(r => r.Query()).Throws(new InvalidOperationException("sin conexión"));

        var result = await repo.Object.CountByAsync(i => i.Clave, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("DATABASE_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task TopAsync_filtra_ordena_y_limita_en_la_consulta()
    {
        var repo = await CrearRepositorioAsync();

        var result = await repo.TopAsync(i => i.Id, i => i.Orden, take: 2, predicate: i => i.Id != 1, ct: TestContext.Current.CancellationToken);
        var asc = await repo.TopAsync(i => i.Id, i => i.Orden, take: 1, descending: false, ct: TestContext.Current.CancellationToken);

        Assert.Equal([3, 2], result.Value);
        Assert.Equal([2], asc.Value);
    }
}
