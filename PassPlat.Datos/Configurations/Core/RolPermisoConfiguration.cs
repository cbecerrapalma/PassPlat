using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("RolesPermisos");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.IdRol).IsRequired();
        builder.Property(r => r.IdPermiso).IsRequired();
        builder.Property(r => r.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(r => r.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(r => r.FecMod).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(r => r.Rol)
            .WithMany(ro => ro.RolesPermisos)
            .HasForeignKey(r => r.IdRol)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Permiso)
            .WithMany(p => p.RolesPermisos)
            .HasForeignKey(r => r.IdPermiso)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.UsrMod)
            .WithMany(u => u.RolesPermisosModificados)
            .HasForeignKey(r => r.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.IdRol, r.IdPermiso }).HasFilter("Activo = 1").IsUnique().HasDatabaseName("UX_RolesPermisos_Activo");
    }
}
