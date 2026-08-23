using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ConfSamlConfiguration : IEntityTypeConfiguration<ConfSaml>
{
    public void Configure(EntityTypeBuilder<ConfSaml> builder)
    {
        builder.ToTable("ConfSaml");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.IdTenant).IsRequired();
        builder.Property(c => c.EntityId).HasMaxLength(500).IsRequired();
        builder.Property(c => c.MetadataUrl).HasMaxLength(1000);
        builder.Property(c => c.MetadataXml).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Certificate).HasMaxLength(2000);
        builder.Property(c => c.SignatureAlgorithm).HasMaxLength(200);
        builder.Property(c => c.DigestAlgorithm).HasMaxLength(200);
        builder.Property(c => c.SsoUrl).HasMaxLength(1000);
        builder.Property(c => c.SloUrl).HasMaxLength(1000);
        builder.Property(c => c.AttributeEmail).HasMaxLength(500);
        builder.Property(c => c.AttributeNombre).HasMaxLength(500);
        builder.Property(c => c.AttributeUid).HasMaxLength(500);
        builder.Property(c => c.WantsAssertionsSigned).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.AutenticacionRequestSigned).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.AllowCreate).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.AutoProvisionar).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Estado).HasDefaultValue((byte)1).IsRequired();
        builder.Property(c => c.Metadata).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(c => c.FecMod);

        builder.HasOne(c => c.Tenant)
            .WithMany(t => t.ConfSamls)
            .HasForeignKey(c => c.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.IdTenant).IsUnique().HasDatabaseName("UK_ConfSaml_Tenant");
        builder.HasIndex(c => c.EntityId).HasDatabaseName("IX_ConfSaml_EntityId");
        builder.HasIndex(c => c.Activo).HasFilter("Activo = 1").HasDatabaseName("IX_ConfSaml_Activo");
    }
}
