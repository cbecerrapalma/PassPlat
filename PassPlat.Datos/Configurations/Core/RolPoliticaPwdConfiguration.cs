using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class RolPoliticaPwdConfiguration : IEntityTypeConfiguration<RolPoliticaPwd>
{
    public void Configure(EntityTypeBuilder<RolPoliticaPwd> builder)
    {
        builder.ToTable("RolesPoliticasPwd");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.IdTenant).IsRequired();
        builder.Property(r => r.IdRol).IsRequired();
        builder.Property(r => r.IdPolitica).IsRequired();
        builder.Property(r => r.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(r => r.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(r => r.FecMod).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.RolesPoliticasPwd)
            .HasForeignKey(r => r.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Rol)
            .WithMany(ro => ro.RolesPoliticasPwd)
            .HasForeignKey(r => r.IdRol)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Politica)
            .WithMany(p => p.RolesPoliticasPwd)
            .HasForeignKey(r => r.IdPolitica)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.UsrMod)
            .WithMany(u => u.RolesPoliticasPwdModificadas)
            .HasForeignKey(r => r.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IdTenant, r.IdRol }).HasFilter("Activo = 1").IsUnique().HasDatabaseName("UX_RolesPol_Tenant_Activo");
    }
}
