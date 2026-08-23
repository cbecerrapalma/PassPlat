using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ConfigTenantConfiguration : IEntityTypeConfiguration<ConfigTenant>
{
    public void Configure(EntityTypeBuilder<ConfigTenant> builder)
    {
        builder.ToTable("ConfigTenants");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Property(c => c.IdTenant).IsRequired();
#pragma warning disable CS0618 // Mapeo legítimo a columna existente en DB
        builder.Property(c => c.MFAObligatorio).HasDefaultValue(false).IsRequired();
#pragma warning restore CS0618
        builder.Property(c => c.TimeoutSesionMin).HasDefaultValue(30).IsRequired();
        builder.Property(c => c.MaxSesionesConc).HasDefaultValue(5).IsRequired();
        builder.Property(c => c.ReqMFA).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.DiasRetAuditoria).HasDefaultValue(365).IsRequired();
        builder.Property(c => c.PepperVersionActual).HasDefaultValue((byte)1).IsRequired();

        builder.HasOne(c => c.Tenant)
            .WithMany(t => t.Configs)
            .HasForeignKey(c => c.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.IdTenant).IsUnique();
    }
}
