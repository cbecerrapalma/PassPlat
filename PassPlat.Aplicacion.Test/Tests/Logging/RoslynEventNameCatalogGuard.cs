using System.Reflection;
using CBP.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PassPlat.Aplicacion.Test.Tests.Logging;

/// <summary>
/// Guard reutilizable de gobernanza de EventName.
/// Catálogo derivado por REFLEXIÓN de <see cref="LoggingEvents"/> (fuente de verdad
/// ejecutable) y escaneo estático con Roslyn (CSharpSyntaxTree): detecta asignaciones
/// `EventName = "literal"` cuyo valor pertenece al catálogo. Los valores dinámicos y
/// los literales no-catálogo se ignoran (modo conservador D5). No depende de
/// Logging.EventCatalog.md para ninguna violación (D4/D5 + precisión del usuario).
/// </summary>
public sealed class RoslynEventNameCatalogGuard : IEventNameCatalogGuard
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", ".git", "node_modules", "TestResults", "artifacts", "coverage", ".vs"
    };

    public IReadOnlyDictionary<string, string> CatalogByValue { get; } = BuildCatalogByReflection();

    private static IReadOnlyDictionary<string, string> BuildCatalogByReflection()
    {
        var fields = typeof(LoggingEvents).GetFields(BindingFlags.Public | BindingFlags.Static);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!field.IsLiteral || field.FieldType != typeof(string))
                continue;
            if (field.GetRawConstantValue() is not string value || value.Length == 0)
                continue;
            map[value] = field.Name;
        }
        return map;
    }

    public IReadOnlyList<EventNameLiteralViolation> Scan(string root, CancellationToken ct = default)
    {
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !IsExcluded(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ScanFiles(files, ct);
    }

    public IReadOnlyList<EventNameLiteralViolation> ScanFiles(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        var violations = new List<EventNameLiteralViolation>();
        foreach (var file in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            violations.AddRange(ScanFile(file));
        }
        return violations;
    }

    private static bool IsExcluded(string path)
    {
        var segments = path.Split(new char[] { '\\', '/' });
        return segments.Any(seg => ExcludedDirectories.Contains(seg));
    }

    private IReadOnlyList<EventNameLiteralViolation> ScanFile(string filePath)
    {
        string source;
        try
        {
            source = File.ReadAllText(filePath);
        }
        catch
        {
            return Array.Empty<EventNameLiteralViolation>();
        }

        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var root = tree.GetRoot();
        var result = new List<EventNameLiteralViolation>();

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) == false)
                continue;
            if (IsEventNameReference(assignment.Left) == false)
                continue;
            if (assignment.Right is not LiteralExpressionSyntax literal ||
                literal.IsKind(SyntaxKind.StringLiteralExpression) == false)
                continue;

            var value = literal.Token.ValueText;
            if (value.Length == 0)
                continue;
            if (CatalogByValue.TryGetValue(value, out var constantName))
            {
                var line = tree.GetLineSpan(literal.Span).StartLinePosition.Line + 1;
                result.Add(new EventNameLiteralViolation(filePath, line, value, constantName));
            }
        }

        return result;
    }

    private static bool IsEventNameReference(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "EventName",
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "EventName",
        _ => false
    };
}