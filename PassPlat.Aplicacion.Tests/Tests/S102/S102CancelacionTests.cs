using Microsoft.EntityFrameworkCore;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;

namespace PassPlat.Aplicacion.Tests.Tests.S102;

/// <summary>
/// S102 · vuelta 3 — un request que el cliente abortó (p. ej. al navegar a otra página) no es
/// DB_ERROR: los repositorios dejan pasar la OperationCanceledException para que el pipeline
/// la trate como cancelación y no responda 500.
/// </summary>
public sealed class S102CancelacionTests
{
    private static PassPlatDbContext Contexto() =>
        new(new DbContextOptionsBuilder<PassPlatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CancellationToken Cancelado()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    [Fact]
    public async Task Accesos_por_rol_con_request_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new AccesoRepository(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ObtenerAccesosPorRolAsync(1, Cancelado()));
    }

    [Fact]
    public async Task Accesos_por_usuario_con_request_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new AccesoRepository(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ObtenerAccesosUsuarioAsync(1, Cancelado()));
    }

    [Fact]
    public async Task Permisos_por_rol_con_request_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new RolPermisoRepository(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ObtenerPermisosPorRolAsync(1, Cancelado()));
    }

    [Fact]
    public async Task Accesos_por_rol_sin_cancelar_sigue_devolviendo_la_lista()
    {
        await using var db = Contexto();
        var repo = new AccesoRepository(db);

        var resultado = await repo.ObtenerAccesosPorRolAsync(1, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }
}
