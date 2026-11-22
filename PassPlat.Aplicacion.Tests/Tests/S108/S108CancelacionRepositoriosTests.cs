using CBP.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Aplicacion.Tests.Tests.S108;

/// <summary>
/// S108 · D1 — una cancelación no es DB_ERROR ni en los métodos heredados de CBP (RepositoryAsync)
/// ni en los repositorios propios de PassPlat: la OperationCanceledException sube al pipeline.
/// </summary>
public sealed class S108CancelacionRepositoriosTests
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
    public async Task WhereAsync_heredado_de_CBP_con_token_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new RolRepository(db, Mock.Of<IUnitOfWorkAsync>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.WhereAsync(r => r.Activo, true, Cancelado()));
    }

    [Fact]
    public async Task CountAsync_heredado_de_CBP_con_token_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new RolRepository(db, Mock.Of<IUnitOfWorkAsync>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.CountAsync(Cancelado()));
    }

    [Fact]
    public async Task Repositorio_de_PassPlat_con_token_cancelado_propaga_la_cancelacion()
    {
        await using var db = Contexto();
        var repo = new RolesHerenciaRepository(db);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ObtenerPadresAsync(1, Cancelado()));
    }

    [Fact]
    public async Task WhereAsync_heredado_de_CBP_sin_cancelar_sigue_devolviendo_Success()
    {
        await using var db = Contexto();
        db.Set<Rol>().Add(new Rol { Id = 1, Codigo = "S108", Nombre = "S108" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new RolRepository(db, Mock.Of<IUnitOfWorkAsync>());

        var r = await repo.WhereAsync(x => x.Activo, true, TestContext.Current.CancellationToken);

        Assert.True(r.IsSuccess);
        Assert.Single(r.Value);
    }
}
