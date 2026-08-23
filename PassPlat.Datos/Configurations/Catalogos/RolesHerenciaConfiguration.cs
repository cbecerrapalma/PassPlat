using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class RolesHerenciaConfiguration : IEntityTypeConfiguration<RolesHerencia>
{
    public void Configure(EntityTypeBuilder<RolesHerencia> builder)
    {
        builder.ToTable("RolesHerencia", tb => tb.HasTrigger("TR_RolesHerencia_ValidarTenant"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.IdRolHijo).IsRequired();
        builder.Property(r => r.IdRolPadre).IsRequired();
        builder.Property(r => r.IdTenant).IsRequired();
        builder.Property(r => r.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(r => r.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(r => r.RolHijo)
            .WithMany(r => r.RolesHerenciaHijos)
            .HasForeignKey(r => r.IdRolHijo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RolPadre)
            .WithMany(r => r.RolesHerenciaPadres)
            .HasForeignKey(r => r.IdRolPadre)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IdRolHijo, r.IdRolPadre }).IsUnique();
    }
}
