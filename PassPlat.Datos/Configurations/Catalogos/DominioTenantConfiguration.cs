using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class DominioTenantConfiguration : IEntityTypeConfiguration<DominioTenant>
{
    public void Configure(EntityTypeBuilder<DominioTenant> builder)
    {
        builder.ToTable("DominiosTenant");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.IdTenant).IsRequired();
        builder.Property(d => d.Dominio).HasMaxLength(100).IsRequired();

        builder.HasOne(d => d.Tenant)
            .WithMany(t => t.Dominios)
            .HasForeignKey(d => d.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.IdTenant, d.Dominio }).IsUnique();
    }
}
