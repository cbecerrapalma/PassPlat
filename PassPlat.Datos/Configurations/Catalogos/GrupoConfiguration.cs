using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class GrupoConfiguration : IEntityTypeConfiguration<Grupo>
{
    public void Configure(EntityTypeBuilder<Grupo> builder)
    {
        builder.ToTable("Grupos");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).ValueGeneratedOnAdd();
        builder.Property(g => g.IdTenant).IsRequired();
        builder.Property(g => g.Codigo).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(g => g.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Descripcion).HasMaxLength(200);
        builder.Property(g => g.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(g => g.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(g => g.Tenant)
            .WithMany()
            .HasForeignKey(g => g.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.IdTenant, g.Codigo }).IsUnique();
    }
}
