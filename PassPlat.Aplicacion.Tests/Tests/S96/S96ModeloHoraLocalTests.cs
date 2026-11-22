using Microsoft.EntityFrameworkCore;
using PassPlat.Datos;

namespace PassPlat.Aplicacion.Tests.Tests.S96;

/// <summary>
/// S96.1 · D2 — los DEFAULT de fecha del modelo EF son hora local, igual que la BD.
/// </summary>
public class S96ModeloHoraLocalTests
{
    [Fact]
    public void Modelo_DefaultsDeFecha_SinUtc()
    {
        using var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var defaults = ctx.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => (Propiedad: $"{e.ClrType.Name}.{p.Name}", Sql: p.GetDefaultValueSql())))
            .Where(x => x.Sql is not null)
            .ToList();

        Assert.NotEmpty(defaults);
        Assert.DoesNotContain(defaults, x => x.Sql!.Contains("utc", StringComparison.OrdinalIgnoreCase));
    }
}
