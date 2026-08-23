using System.Reflection;

namespace PassPlat.CBP.Architecture.Test;

/// <summary>
/// Tests de arquitectura CBP — skeleton S24.2 sobre CBP-Dependency-Rules.md:
/// D-01 dirección, D-02 ciclos, D-03 acoplamiento por clasificación, D-04 API pública,
/// D-05 FrameworkReference, D-06 transitiva no crea permiso.
/// </summary>
public sealed class DependencyBoundaryTests
{
    // ---------------------------------------------------------------- helpers

    private static string OutputDir => AppContext.BaseDirectory;

    private static string DllPath(string simpleName) => Path.Combine(OutputDir, $"{simpleName}.dll");

    /// <summary>Carga el assembly real desde el output (GetReferencedAssemblies solo lee metadata, no resuelve tipos).</summary>
    private static Assembly Load(string simpleName)
    {
        var path = DllPath(simpleName);
        Assert.True(File.Exists(path), $"No existe {path} en el output del proyecto de tests.");
        return Assembly.LoadFrom(path);
    }

    private static IEnumerable<string> ReferencedSimpleNames(string simpleName)
        => Load(simpleName).GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    private static IEnumerable<Type> PublicTypes(string simpleName)
    {
        foreach (var t in Load(simpleName).GetExportedTypes())
            yield return t;
    }

    /// <summary>Recolecta los full names de TODOS los tipos a los que una API pública de un ensamblado hace referencia en su firma.</summary>
    private static HashSet<string> PublicApiReferencedTypes(string simpleName)
    {
        var names = new HashSet<string>();
        foreach (var type in PublicTypes(simpleName))
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
                    case PropertyInfo prop:
                        Collect(prop.PropertyType);
                        break;
                    case FieldInfo field:
                        Collect(field.FieldType);
                        break;
                    case EventInfo ev:
                        Collect(ev.EventHandlerType);
                        break;
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

    private static void AssertForbiddenPrefixesAbsent(
        string context,
        IEnumerable<string> names,
        IEnumerable<string> forbiddenPrefixes)
    {
        var violations = names
            .Where(n => forbiddenPrefixes.Any(f => n.StartsWith(f, StringComparison.Ordinal)))
            .OrderBy(n => n)
            .Distinct()
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{context} viola D-03/D-04: {violations.Count} tipos/assemblies prohibidos detectados:\n  {string.Join("\n  ", violations)}");
    }

    // ---------------------------------------------------------------- D-01 dirección

    [Fact]
    public void D01_Ningun_Assembly_CBP_Referencia_PassPlat()
    {
        foreach (var asm in CbpCatalog.All)
        {
            var referenced = ReferencedSimpleNames(asm);
            var passplatrefs = referenced.Where(r => r.StartsWith("PassPlat", StringComparison.Ordinal)).ToList();
            Assert.True(
                passplatrefs.Count == 0,
                $"D-01 violado: {asm} referencia PassPlat.* -> {string.Join(", ", passplatrefs)}");
        }
    }

    // ---------------------------------------------------------------- D-02 ciclos

    [Fact]
    public void D02_Grafo_De_Dependencias_No_Tiene_Ciclos()
    {
        var adjacency = CbpCatalog.All.ToDictionary(name => name, name => ReferencedSimpleNames(name).Where(r => CbpCatalog.All.Contains(r)).ToList());
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();
        var path = new List<string>();

        foreach (var start in CbpCatalog.All)
            Visit(start);

        void Visit(string node)
        {
            if (visited.Contains(node)) return;
            if (!visiting.Add(node)) throw new InvalidOperationException($"Ciclo en grafo CBP: {string.Join(" -> ", path)} -> {node}");
            path.Add(node);
            foreach (var dep in adjacency[node])
                Visit(dep);
            path.RemoveAt(path.Count - 1);
            visiting.Remove(node);
            visited.Add(node);
        }
    }

    // ---------------------------------------------------------------- D-03 acoplamiento por clasificación (nivel metadata)

    [Theory]
    [InlineData("CBP")]
    [InlineData("CBP.Results")]
    public void D03_Core_No_Referencia_Tecnologias_Prohibidas(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-03 CORE] {asm}",
            ReferencedSimpleNames(asm),
            CbpCatalog.ForbiddenAssemblyPrefixes["CORE"]);

    [Theory]
    [InlineData("CBP.Events")]
    [InlineData("CBP.Caching.Abstractions")]
    public void D03_CoreCrosscutting_No_Referencia_Tecnologias_Prohibidas(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-03 CORE-CROSSCUTTING] {asm}",
            ReferencedSimpleNames(asm),
            CbpCatalog.ForbiddenAssemblyPrefixes["CORE-CROSSCUTTING"]);

    [Theory]
    [InlineData("CBP.Services.Abstractions")]
    [InlineData("CBP.Services.Async")]
    [InlineData("CBP.Services.Sync")]
    public void D03_Application_No_Referencia_Tecnologias_Prohibidas(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-03 APPLICATION] {asm}",
            ReferencedSimpleNames(asm),
            CbpCatalog.ForbiddenAssemblyPrefixes["APPLICATION"]);

    [Theory]
    [InlineData("CBP.Data.Abstractions")]
    [InlineData("CBP.Data.Asynchronous")]
    [InlineData("CBP.Data.Synchronous")]
    [InlineData("CBP.Data.Specifications")]
    [InlineData("CBP.Data.Utilities")]
    public void D03_Data_No_Referencia_Tecnologias_Prohibidas(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-03 DATA] {asm}",
            ReferencedSimpleNames(asm),
            CbpCatalog.ForbiddenAssemblyPrefixes["DATA"]);

    [Theory]
    [InlineData("CBP.Caching.Memory")]
    [InlineData("CBP.Caching.NCache")]
    [InlineData("CBP.Caching.Redis")]
    [InlineData("CBP.MultiTenant")]
    [InlineData("CBP.Security.Cryptography")]
    [InlineData("CBP.Logging")]
    [InlineData("CBP.Emails")]
    [InlineData("CBP.Excel")]
    [InlineData("CBP.Authentication.Abstractions")]
    [InlineData("CBP.Authentication.JwtBearer")]
    [InlineData("CBP.WebApi")]
    public void D03_Infrastructure_No_Referencia_PassPlat(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-03 INFRASTRUCTURE] {asm}",
            ReferencedSimpleNames(asm),
            CbpCatalog.ForbiddenAssemblyPrefixes["INFRASTRUCTURE"]);

    // ---------------------------------------------------------------- D-04 API pública expuesta

    [Theory]
    [InlineData("CBP")]
    [InlineData("CBP.Results")]
    public void D04_Core_ApiPublica_No_Expone_Tipos_Prohibidos(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-04 CORE] {asm}",
            PublicApiReferencedTypes(asm),
            CbpCatalog.ForbiddenTypeNamePrefixes["CORE"]);

    [Theory]
    [InlineData("CBP.Events")]
    [InlineData("CBP.Caching.Abstractions")]
    public void D04_CoreCrosscutting_ApiPublica_No_Expone_Tipos_Prohibidos(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-04 CORE-CROSSCUTTING] {asm}",
            PublicApiReferencedTypes(asm),
            CbpCatalog.ForbiddenTypeNamePrefixes["CORE-CROSSCUTTING"]);

    [Theory]
    [InlineData("CBP.Services.Async")]
    [InlineData("CBP.Services.Sync")]
    public void D04_Application_ApiPublica_No_Expone_EF_Ni_Infra(string asm)
        => AssertForbiddenPrefixesAbsent(
            $"[D-04 APPLICATION] {asm}",
            PublicApiReferencedTypes(asm),
            CbpCatalog.ForbiddenTypeNamePrefixes["APPLICATION"]);

    // ---------------------------------------------------------------- D-05 FrameworkReference

    [Theory]
    [InlineData("CBP")]
    [InlineData("CBP.Results")]
    [InlineData("CBP.Events")]
    [InlineData("CBP.Caching.Abstractions")]
    [InlineData("CBP.Services.Abstractions")]
    [InlineData("CBP.Services.Async")]
    [InlineData("CBP.Services.Sync")]
    [InlineData("CBP.Data.Abstractions")]
    [InlineData("CBP.Data.Asynchronous")]
    [InlineData("CBP.Data.Synchronous")]
    [InlineData("CBP.Data.Specifications")]
    [InlineData("CBP.Data.Utilities")]
    public void D05_Core_Data_Application_Sin_FrameworkReference_AspNet(string asm)
    {
        var aspref = ReferencedSimpleNames(asm).Where(r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)).ToList();
        Assert.True(
            aspref.Count == 0,
            $"D-05 violado: {asm} referencia ASP.NET Core -> {string.Join(", ", aspref)}");
    }

    [Theory]
    [InlineData("CBP.Authentication.Abstractions")]
    [InlineData("CBP.Authentication.JwtBearer")]
    [InlineData("CBP.WebApi")]
    public void D05_Infrastructure_Auth_WebApi_Si_Referencian_AspNet(string asm)
    {
        var aspref = ReferencedSimpleNames(asm).Where(r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)).ToList();
        Assert.True(
            aspref.Count > 0,
            $"D-05 violado: {asm} debería referenciar ASP.NET Core (clasificación infraestructura/auth/webapi)");
    }

    // ---------------------------------------------------------------- D-06 transitiva no crea permiso

    [Fact]
    public void D06_Application_EF_Transitivo_No_Crea_Acceso_Directo_A_Tipos_EF()
    {
        foreach (var asm in new[] { "CBP.Services.Async", "CBP.Services.Sync" })
        {
            var referenced = ReferencedSimpleNames(asm).ToList();
            var transitivoEF = referenced.Contains("Microsoft.EntityFrameworkCore");
            var apiTypes = PublicApiReferencedTypes(asm);
            var efUsed = apiTypes.Where(t => t.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)).ToList();

            Assert.True(
                efUsed.Count == 0,
                $"[D-06] {asm}: usa tipos EF en su API expuesta aunque sea transitivo -> {string.Join(", ", efUsed)}");

            // Documenta el estado: puede haber referencia de assembly transitiva (F6) pero cero uso de tipos EF.
            if (!transitivoEF)
            {
                // Sin EF en firmas ni en referencias: la regla D-03 de ensamblados ya cubre el caso.
            }
        }
    }
}