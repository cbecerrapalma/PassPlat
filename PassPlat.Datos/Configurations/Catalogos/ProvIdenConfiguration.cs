using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Models;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ProvIdenConfiguration : IEntityTypeConfiguration<ProvIden>
{
    public void Configure(EntityTypeBuilder<ProvIden> builder)
    {
        builder.ToTable("ProvIden");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.TipoProveedor).IsRequired();
        builder.Property(p => p.Protocolo).HasMaxLength(20);
        builder.Property(p => p.Version).HasMaxLength(50);
        builder.Property(p => p.UrlIssuer).HasMaxLength(500);
        builder.Property(p => p.EndpointAutorizacion).HasMaxLength(500);
        builder.Property(p => p.EndpointToken).HasMaxLength(500);
        builder.Property(p => p.EndpointUserInfo).HasMaxLength(500);
        builder.Property(p => p.JwksUri).HasMaxLength(500);
        builder.Property(p => p.EndpointRevocacion).HasMaxLength(500);
        builder.Property(p => p.SoportaPKCE).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.SoportaRefreshToken).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.SoportaMFA).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.Icono).HasMaxLength(50);
        builder.Property(p => p.Orden).HasDefaultValue((short)0).IsRequired();
        builder.Property(p => p.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(p => p.FecMod);

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UK_ProvIden_Codigo");
        builder.HasIndex(p => p.Activo).HasFilter("Activo = 1").HasDatabaseName("IX_ProvIden_Activo");
        builder.HasIndex(p => p.TipoProveedor).HasDatabaseName("IX_ProvIden_TipoProveedor");
        builder.HasIndex(p => p.Protocolo).HasFilter("Protocolo IS NOT NULL").HasDatabaseName("IX_ProvIden_Protocolo");

        builder.Property(p => p.Metadata)
            .HasConversion(new ValueConverter<OAuthProviderMetadata?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<OAuthProviderMetadata>(v, (JsonSerializerOptions?)null)))
            .HasColumnType("nvarchar(max)");
    }
}
