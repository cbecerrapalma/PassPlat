using Microsoft.EntityFrameworkCore;
using PassPlat.Datos;
using PassPlat.Datos.Repositories;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Aplicacion.Tests.Tests.S95;

/// <summary>
/// S95 · P47 — la lista de configuraciones de proveedor se busca en la consulta (proveedor,
/// Client ID o tenant) y se acota al tenant pedido; sin tenant devuelve todas.
/// </summary>
public class S95ConfProvIdenBusquedaTests
{
    private sealed record Semilla(ConfProvIdenRepository Repo, int IdTenantA, int IdTenantB);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Semilla Sembrar()
    {
        var ctx = new PassPlatDbContext(new DbContextOptionsBuilder<PassPlatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tenantA = new Tenant { Codigo = "ABARROTES", Nombre = "Abarrotes del Sur" };
        var tenantB = new Tenant { Codigo = "VESTUARIO", Nombre = "Vestuario SA de CV" };
        var google = new ProvIden { Codigo = "GOOGLE", Nombre = "Google" };
        var microsoft = new ProvIden { Codigo = "MICROSOFT", Nombre = "Microsoft" };
        ctx.Tenants.AddRange(tenantA, tenantB);
        ctx.Set<ProvIden>().AddRange(google, microsoft);
        ctx.SaveChanges();

        ctx.Set<ConfProvIden>().AddRange(
            ConfProvIden.Crear(tenantA.Id, google.Id, "cliente-a-google", "secreto", "https://a.test/callback"),
            ConfProvIden.Crear(tenantA.Id, microsoft.Id, "cliente-a-microsoft", "secreto", "https://a.test/callback"),
            ConfProvIden.Crear(tenantB.Id, google.Id, "cliente-b-google", "secreto", "https://b.test/callback"));
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        return new Semilla(new ConfProvIdenRepository(ctx), tenantA.Id, tenantB.Id);
    }

    [Fact]
    public async Task BuscarPaginado_con_tenant_devuelve_solo_sus_configuraciones_con_nombres()
    {
        var s = Sembrar();

        var r = await s.Repo.BuscarPaginadoAsync(s.IdTenantA, null, 1, 20, Ct);

        Assert.True(r.IsSuccess);
        Assert.Equal(2, r.Value.TotalCount);
        Assert.All(r.Value.Items, c =>
        {
            Assert.Equal(s.IdTenantA, c.IdTenant);
            Assert.Equal("Abarrotes del Sur", c.Tenant?.Nombre);
            Assert.NotNull(c.ProvIden);
        });
    }

    [Fact]
    public async Task BuscarPaginado_sin_tenant_devuelve_todas_y_pagina()
    {
        var s = Sembrar();

        var todas = await s.Repo.BuscarPaginadoAsync(null, null, 1, 20, Ct);
        var pagina2 = await s.Repo.BuscarPaginadoAsync(null, null, 2, 2, Ct);

        Assert.Equal(3, todas.Value.TotalCount);
        Assert.Equal(3, pagina2.Value.TotalCount);
        Assert.Single(pagina2.Value.Items);
    }

    [Fact]
    public async Task BuscarPaginado_busca_por_proveedor_client_id_y_tenant()
    {
        var s = Sembrar();

        var porProveedor = await s.Repo.BuscarPaginadoAsync(null, "Microsoft", 1, 20, Ct);
        var porClientId = await s.Repo.BuscarPaginadoAsync(null, "cliente-b", 1, 20, Ct);
        var porTenant = await s.Repo.BuscarPaginadoAsync(null, "Vestuario", 1, 20, Ct);
        var fueraDelTenant = await s.Repo.BuscarPaginadoAsync(s.IdTenantA, "cliente-b", 1, 20, Ct);

        Assert.Equal("cliente-a-microsoft", Assert.Single(porProveedor.Value.Items).ClientId);
        Assert.Equal(s.IdTenantB, Assert.Single(porClientId.Value.Items).IdTenant);
        Assert.Equal(s.IdTenantB, Assert.Single(porTenant.Value.Items).IdTenant);
        Assert.Empty(fueraDelTenant.Value.Items);
    }
}
