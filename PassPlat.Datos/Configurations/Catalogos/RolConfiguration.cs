using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.IdTenant);
        builder.Property(r => r.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(r => r.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Descripcion).HasMaxLength(200);
        builder.Property(r => r.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(r => r.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.Roles)
            .HasForeignKey(r => r.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IdTenant, r.Codigo }).IsUnique();
    }
}
