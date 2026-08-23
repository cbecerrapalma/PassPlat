using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CBP.Data.Abstractions;
using CBP.Data.Asynchronous;

namespace PassPlat.CBP.Architecture.Test;

/// <summary>
/// Tests de contrato S25.2 — EF-free Data Abstractions with Async/Sync parity.
/// S25.2-CONTRACT-PURITY: CBP.Data.Abstractions.dll no referencia EF y su API pública
/// no expone DbContext/DbSet/EntityEntry/IDbContext*. Asynchronous y Synchronous sí
/// referencian EF. DI resuelve el contrato no genérico → implementación EF genérica.
/// </summary>
public sealed class S25_2ContractPurityTests
{
    // ---------------------------------------------------------------- helpers

    private static string DllPath(string simpleName) => Path.Combine(AppContext.BaseDirectory, $"{simpleName}.dll");

    private static Assembly Load(string simpleName)
    {
        var path = DllPath(simpleName);
        Assert.True(File.Exists(path), $"No existe {path} en el output del proyecto de tests.");
        return Assembly.LoadFrom(path);
    }

    private static IEnumerable<string> ReferencedSimpleNames(string simpleName)
        => Load(simpleName).GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    private static IEnumerable<string> PublicApiTypeFullNames(string simpleName)
    {
        var names = new List<string>();
        foreach (var type in Load(simpleName).GetExportedTypes())
        {
            Collect(type);
            foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                switch (m)
                {
                    case MethodInfo method:
                        Collect(method.ReturnType);
                        foreach (var p in method.GetParameters()) Collect(p.ParameterType);
                        break;
                    case PropertyInfo prop: Collect(prop.PropertyType); break;
                    case FieldInfo field: Collect(field.FieldType); break;
                    case EventInfo ev: Collect(ev.EventHandlerType); break;
                }
            }
        }
        return names;

        void Collect(Type? t)
        {
            if (t is null) return;
            if (t.IsGenericType)
            {
                names.Add(t.GetGenericTypeDefinition().FullName ?? t.Name);
                foreach (var arg in t.GetGenericArguments()) Collect(arg);
            }
            else if (t.IsArray)
            {
                names.Add($"{t.GetElementType()!.FullName}[]");
                Collect(t.GetElementType());
            }
            else
            {
                if (!string.IsNullOrEmpty(t.FullName)) names.Add(t.FullName);
            }
        }
    }

    // ---------------------------------------------------------------- CONTRACT PURITY

    /// <summary>
    /// S25.2 — CBP.Data.Abstractions.dll NO referencia Microsoft.EntityFrameworkCore
    /// ni Microsoft.Data.SqlClient (verificación sobre assembly metadata).
    /// </summary>
    [Fact]
    public void DataAbstractions_No_Referencia_EF_Ni_SqlClient()
    {
        var referenced = ReferencedSimpleNames("CBP.Data.Abstractions");
        var forbidden = referenced
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
                     || n.StartsWith("System.Data.SqlClient", StringComparison.Ordinal))
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"CBP.Data.Abstractions referencia tecnologías prohibidas (F1 no resuelto): {string.Join(", ", forbidden)}");
    }

    /// <summary>
    /// S25.2 — La API pública de CBP.Data.Abstractions no expone tipos EF
    /// (DbContext, DbSet, EntityEntry, IDbContext*, Microsoft.EntityFrameworkCore.*).
    /// </summary>
    [Fact]
    public void DataAbstractions_ApiPublica_No_Expone_Tipos_EF()
    {
        var apiTypes = PublicApiTypeFullNames("CBP.Data.Abstractions");
        var forbidden = apiTypes
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .OrderBy(n => n)
            .Distinct()
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"CBP.Data.Abstractions expone tipos EF en su API pública: {string.Join(", ", forbidden)}");

        // Verificación explícita de los tipos nominales prohibidos por S25.1.
        foreach (var t in new[] { "Microsoft.EntityFrameworkCore.DbContext", "Microsoft.EntityFrameworkCore.DbSet" })
        {
            Assert.DoesNotContain(apiTypes, n => n.StartsWith(t, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// S25.2 — CBP.Data.Asynchronous.dll SÍ referencia EF (constraint vive en la
    /// implementación, donde corresponde).
    /// </summary>
    [Fact]
    public void DataAsynchronous_Si_Referencia_EF()
    {
        var referenced = ReferencedSimpleNames("CBP.Data.Asynchronous");
        Assert.True(
            referenced.Contains("Microsoft.EntityFrameworkCore"),
            "CBP.Data.Asynchronous debería referenciar Microsoft.EntityFrameworkCore (la implementación EF genérica vive aquí).");
    }

    /// <summary>
    /// S25.2 — CBP.Data.Synchronous.dll SÍ referencia EF (constraint vive en la
    /// implementación, donde corresponde).
    /// </summary>
    [Fact]
    public void DataSynchronous_Si_Referencia_EF()
    {
        var referenced = ReferencedSimpleNames("CBP.Data.Synchronous");
        Assert.True(
            referenced.Contains("Microsoft.EntityFrameworkCore.Relational"),
            "CBP.Data.Synchronous debería referenciar Microsoft.EntityFrameworkCore.Relational (la implementación EF genérica vive aquí).");
    }

    // ---------------------------------------------------------------- DI (contrato no genérico → impl EF genérica)

    /// <summary>
    /// S25.2 — CBP.Data.Abstractions no contiene ningún uso del tipo genérico
    /// IUnitOfWorkAsync&lt;TDbContext&gt; (el contrato es ahora no genérico).
    /// </summary>
    [Fact]
    public void IUnitOfWorkAsync_Es_No_Generico()
    {
        var t = typeof(IUnitOfWorkAsync);
        Assert.False(t.IsGenericTypeDefinition, "IUnitOfWorkAsync debe ser no genérico tras S25");
        Assert.True(t.IsInterface, "IUnitOfWorkAsync debe ser interfaz");
        Assert.True(typeof(IDisposable).IsAssignableFrom(t), "IUnitOfWorkAsync debe implementar IDisposable");
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(t), "IUnitOfWorkAsync debe implementar IAsyncDisposable");
    }

    /// <summary>
    /// S25.2 — IUnitOfWorkSync es no genérico.
    /// </summary>
    [Fact]
    public void IUnitOfWorkSync_Es_No_Generico()
    {
        var t = typeof(IUnitOfWorkSync);
        Assert.False(t.IsGenericTypeDefinition, "IUnitOfWorkSync debe ser no genérico tras S25");
        Assert.True(t.IsInterface, "IUnitOfWorkSync debe ser interfaz");
        Assert.True(typeof(IDisposable).IsAssignableFrom(t), "IUnitOfWorkSync debe implementar IDisposable");
    }

    /// <summary>
    /// S25.2 — El contrato retiró: DbContext, GetRepository, GetCustomRepository,
    /// Begin/Commit/Rollback, HasChanges, RejectChanges. Sólo queda la superficie S25.1.
    /// </summary>
    [Fact]
    public void IUnitOfWorkAsync_Contrato_Retiro_Miembros_S25()
    {
        var members = typeof(IUnitOfWorkAsync).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();

        // Miembros retirados en S25.1 (no deben existir en el contrato).
        foreach (var retired in new[]
                 {
                     "DbContext", "GetRepository", "GetCustomRepository",
                     "BeginTransactionAsync", "CommitTransactionAsync", "RollbackTransactionAsync",
                     "HasChanges", "RejectChanges"
                 })
        {
            Assert.False(members.Contains(retired), $"IUnitOfWorkAsync no debe exponer '{retired}' tras S25");
        }

        // Miembros conservados en el contrato (S25.1-Design §2).
        foreach (var kept in new[] { "RawQuery", "SaveChangesAsync", "SaveEntitiesAsync", "ExecuteInTransactionAsync" })
        {
            Assert.True(members.Contains(kept), $"IUnitOfWorkAsync debe conservar '{kept}'");
        }
    }

    /// <summary>
    /// S25.2 — DI: AddUnitOfWorkAsync&lt;TDbContext&gt; registra IUnitOfWorkAsync (no genérico)
    /// y resuelve a UnitOfWorkAsync&lt;PassPlatDbContext&gt; vía el DbContext del scope.
    /// </summary>
    [Fact]
    public async Task DI_Resuelve_IUnitOfWorkAsync_Desde_UnitOfWorkAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(_ => { });
        services.AddUnitOfWorkAsync<TestDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkAsync>();
        Assert.NotNull(uow);
        Assert.IsType<UnitOfWorkAsync<TestDbContext>>(uow);

        // El DbContext registrado es el mismo que inyecta la implementación.
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var uowConcrete = Assert.IsType<UnitOfWorkAsync<TestDbContext>>(uow);
        Assert.Same(context, uowConcrete.DbContext);
    }

    // ---------------------------------------------------------------- PARITY Async/Sync

    /// <summary>
    /// S25.2 — PARITY funcional: para cada miembro del contrato Async existe su
    /// equivalente funcional en el contrato Sync (sin exigir clones mecánicos).
    /// Detach NO forma parte de la paridad (S25.1 §4: hallazgo, no requisito).
    /// </summary>
    [Fact]
    public void Parity_Async_Sync_Mismos_Miembros_Funcionales()
    {
        var asyncMembers = typeof(IUnitOfWorkAsync).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<MethodInfo>()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToHashSet();

        var syncMembers = typeof(IUnitOfWorkSync).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<MethodInfo>()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToHashSet();

        // Normalización Async→Sync: SaveChangesAsync↔SaveChanges, SaveEntitiesAsync↔SaveEntities,
        // ExecuteInTransactionAsync↔ExecuteInTransaction.
        string SyncEquivalent(string name) => name switch
        {
            "SaveChangesAsync" => "SaveChanges",
            "SaveEntitiesAsync" => "SaveEntities",
            "ExecuteInTransactionAsync" => "ExecuteInTransaction",
            _ => name
        };

        foreach (var m in asyncMembers)
        {
            var equiv = SyncEquivalent(m);
            Assert.True(
                syncMembers.Contains(equiv),
                $"Paridad violada: IUnitOfWorkAsync.{m} no tiene equivalente funcional {equiv} en IUnitOfWorkSync");
        }

        // El retiro de Detach no forma parte de la paridad (S25.1 §4).
        Assert.DoesNotContain("DetachAsync", asyncMembers);
    }
}

/// <summary>
/// DbContext mínimo de prueba — solo verifica la resolución DI del contrato.
/// </summary>
public sealed class TestDbContext : DbContext
{
    public TestDbContext() { }
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
}