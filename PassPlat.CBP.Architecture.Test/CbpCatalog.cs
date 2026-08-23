namespace PassPlat.CBP.Architecture.Test;

/// <summary>
/// Catálogo de ensamblados CBP y su clasificación S23. Puntos únicos de verdad para los tests
/// de arquitectura S24.2 (reglas D-01..D-06 de Docs/Architecture/CBP-Dependency-Rules.md).
/// </summary>
public static class CbpCatalog
{
    public static readonly string[] Core = ["CBP", "CBP.Results"];

    public static readonly string[] CoreCrosscutting = ["CBP.Events", "CBP.Caching.Abstractions"];

    public static readonly string[] Application = ["CBP.Services.Abstractions", "CBP.Services.Async", "CBP.Services.Sync"];

    public static readonly string[] Data =
    [
        "CBP.Data.Abstractions",
        "CBP.Data.Asynchronous",
        "CBP.Data.Synchronous",
        "CBP.Data.Specifications",
        "CBP.Data.Utilities"
    ];

    public static readonly string[] Infrastructure =
    [
        "CBP.Caching.Memory",
        "CBP.Caching.NCache",
        "CBP.Caching.Redis",
        "CBP.MultiTenant",
        "CBP.Security.Cryptography",
        "CBP.Logging",
        "CBP.Emails",
        "CBP.Excel",
        "CBP.Authentication.Abstractions",
        "CBP.Authentication.JwtBearer",
        "CBP.WebApi"
    ];

    public static readonly string[] All = [.. Core, .. CoreCrosscutting, .. Application, .. Data, .. Infrastructure];

    /// <summary>Fronteras tecnológicas por clasificación: prefijos de assembly PROHIBIDOS (D-03).</summary>
    public static IReadOnlyDictionary<string, string[]> ForbiddenAssemblyPrefixes { get; } =
        new Dictionary<string, string[]>
        {
            ["CORE"] =
            [
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient",
                "System.Data.SqlClient",
                "Serilog",
                "StackExchange.Redis",
                "Alachisoft",
                "MailKit",
                "MimeKit",
                "AutoMapper",
                "FluentValidation",
                "Microsoft.AspNetCore",
                "PassPlat",
                "EPPlus"
            ],
            ["CORE-CROSSCUTTING"] =
            [
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient",
                "System.Data.SqlClient",
                "Serilog",
                "StackExchange.Redis",
                "Alachisoft",
                "MailKit",
                "MimeKit",
                "AutoMapper",
                "FluentValidation",
                "Microsoft.AspNetCore",
                "PassPlat",
                "EPPlus"
            ],
            ["APPLICATION"] =
            [
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient",
                "System.Data.SqlClient",
                "Serilog",
                "StackExchange.Redis",
                "Alachisoft",
                "MailKit",
                "MimeKit",
                "Microsoft.AspNetCore",
                "PassPlat",
                "EPPlus"
            ],
            ["DATA"] =
            [
                "Serilog",
                "StackExchange.Redis",
                "Alachisoft",
                "MailKit",
                "MimeKit",
                "AutoMapper",
                "FluentValidation",
                "Microsoft.AspNetCore",
                "PassPlat",
                "EPPlus"
            ],
            ["INFRASTRUCTURE"] =
            [
                "PassPlat"
            ]
        };

    /// <summary>Prefijos de namespace de TIPOS prohibidos por clasificación (D-03/D-04 a nivel símbolo).</summary>
    public static IReadOnlyDictionary<string, string[]> ForbiddenTypeNamePrefixes { get; } =
        new Dictionary<string, string[]>
        {
            ["CORE"] = ["Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient", "Serilog", "StackExchange.Redis", "Alachisoft", "MailKit", "MimeKit", "AutoMapper", "FluentValidation", "Microsoft.AspNetCore", "PassPlat", "OfficeOpenXml"],
            ["CORE-CROSSCUTTING"] = ["Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient", "Serilog", "StackExchange.Redis", "Alachisoft", "MailKit", "MimeKit", "AutoMapper", "FluentValidation", "Microsoft.AspNetCore", "PassPlat", "OfficeOpenXml"],
            ["APPLICATION"] = ["Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient", "Serilog", "StackExchange.Redis", "Alachisoft", "MailKit", "MimeKit", "Microsoft.AspNetCore", "PassPlat", "OfficeOpenXml"],
            ["DATA"] = ["Serilog", "StackExchange.Redis", "Alachisoft", "MailKit", "MimeKit", "AutoMapper", "FluentValidation", "Microsoft.AspNetCore", "PassPlat", "OfficeOpenXml"],
            ["INFRASTRUCTURE"] = ["PassPlat"]
        };

    public static string ClassificationOf(string assemblySimpleName)
    {
        if (Core.Contains(assemblySimpleName)) return "CORE";
        if (CoreCrosscutting.Contains(assemblySimpleName)) return "CORE-CROSSCUTTING";
        if (Application.Contains(assemblySimpleName)) return "APPLICATION";
        if (Data.Contains(assemblySimpleName)) return "DATA";
        if (Infrastructure.Contains(assemblySimpleName)) return "INFRASTRUCTURE";
        throw new ArgumentException($"Ensamblado '{assemblySimpleName}' no está en el catálogo CBP.");
    }
}