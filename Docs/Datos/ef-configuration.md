# EF Core Configuration Patterns

**Location**: `PassPlat.Datos\Configurations\{Catalogos|Core|Contexto}\`

## File Structure

One file per entity: `{EntityName}Configuration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class AppConfiguration : IEntityTypeConfiguration<App>
{
    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.ToTable("Apps");
        builder.HasKey(a => a.Id);
        // ... property mappings
    }
}
```

## Property Type Mapping

| SQL Type | C# Type | EF Configuration |
|----------|---------|------------------|
| `int` | `int` | Default |
| `bigint` | `long` | Default |
| `tinyint` | `byte` | Default or `.HasColumnType("tinyint")` |
| `smallint` | `short` | Default |
| `bit` | `bool` | Default |
| `uniqueidentifier` | `Guid` | Default |
| `nvarchar(n)` | `string` | `.HasMaxLength(n).IsRequired()` |
| `nvarchar(max)` | `string?` | `.HasMaxLength(-1)` |
| `varchar(n)` | `string` | `.HasColumnType("varchar(n)")` |
| `datetime2(n)` | `DateTime` | Default |
| `date` | `DateTime` | `.HasColumnType("date")` |
| `decimal(p,s)` | `decimal` | `.HasPrecision(p, s)` |

## Default Values

**For DB-generated timestamps** — use `HasDefaultValueSql("sysdatetime()")`:
```csharp
builder.Property(t => t.FecCrea).HasDefaultValueSql("sysdatetime()").IsRequired();
```

**For boolean/literal defaults** — use `HasDefaultValue(value)`:
```csharp
builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
```

**NEVER** use `HasDefaultValue(DateTime.UtcNow)` for timestamps — EF will embed the .NET value at migration time, not use SQL's `SYSUTCDATETIME()`.

## Computed Columns (PERSISTED)

```csharp
builder.Property(h => h.AnioMes)
    .HasComputedColumnSql("datepart(year,[FecRegistro])*(100)+datepart(month,[FecRegistro])", stored: true);

builder.Property(h => h.FecRetencion)
    .HasComputedColumnSql("DATEADD(YEAR, 1, FecRegistro)", stored: true);
```

## Indexes

**Descending index** (EF Core 10 syntax):
```csharp
builder.HasIndex(i => new { i.IdUsuario, i.FecAccion })
    .IsDescending(false, true)
    .HasDatabaseName("IX_Auditoria_UsrFec");
```

**Filtered index**:
```csharp
builder.HasIndex(b => new { b.IdUsuario, b.Activo })
    .HasFilter("Activo = 1")
    .HasDatabaseName("IX_Bloqueos_Activo");
```

**Unique filtered index**:
```csharp
builder.HasIndex(h => h.IdUsuario)
    .HasFilter("EsActual = 1")
    .IsUnique()
    .HasDatabaseName("UX_Historial_Actual");
```

## Foreign Keys

Always use `OnDelete(DeleteBehavior.Restrict)`:
```csharp
builder.HasOne(a => a.Usuario)
    .WithMany(u => u.Accesos)
    .HasForeignKey(a => a.IdUsuario)
    .OnDelete(DeleteBehavior.Restrict);
```

Optional FK constraint names:
```csharp
.HasConstraintName("FK_Accesos_Usuario");
```

## Check Constraints

```csharp
builder.HasCheckConstraint("CK_PoliticasPwd_Long",
    "LongMax > LongMin AND LongMin >= 8");
```

## Index Naming Convention

Use `HasDatabaseName()` matching the SQL index name from PASSWORDS.sql:
```csharp
.HasDatabaseName("IX_Intentos_UsrApp")
.HasDatabaseName("UX_Historial_Actual")
```
