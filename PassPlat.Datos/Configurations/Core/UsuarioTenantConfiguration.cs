using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class UsuarioTenantConfiguration : IEntityTypeConfiguration<UsuarioTenant>
{
    public void Configure(EntityTypeBuilder<UsuarioTenant> builder)
    {
        builder.ToTable("UsuarioTenant");
        builder.HasKey(ut => ut.Id);

        builder.Property(ut => ut.Id).ValueGeneratedOnAdd();
        builder.Property(ut => ut.IdUsuario).IsRequired();
        builder.Property(ut => ut.IdTenant).IsRequired();
        builder.Property(ut => ut.IdEstado).IsRequired();
        builder.Property(ut => ut.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(ut => ut.FecAlta).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(ut => ut.FecMod);
        builder.Property(ut => ut.IdUsrMod);

        builder.HasOne(ut => ut.Usuario)
            .WithMany(u => u.UsuarioTenants)
            .HasForeignKey(ut => ut.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ut => ut.Tenant)
            .WithMany(t => t.UsuarioTenants)
            .HasForeignKey(ut => ut.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ut => ut.Estado)
            .WithMany(e => e.UsuarioTenants)
            .HasForeignKey(ut => ut.IdEstado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ut => new { ut.IdUsuario, ut.IdTenant }).IsUnique().HasDatabaseName("UX_UsuarioTenant_Usuario_Tenant");
        builder.HasIndex(ut => new { ut.Id, ut.IdUsuario }).IsUnique().HasDatabaseName("UX_UsuarioTenant_Id_IdUsuario");
        builder.HasIndex(ut => ut.IdUsuario).HasDatabaseName("IX_UsuarioTenant_Usuario");
        builder.HasIndex(ut => new { ut.IdTenant, ut.Activo, ut.IdEstado }).HasDatabaseName("IX_UsuarioTenant_Tenant_Estado");
        builder.HasIndex(ut => ut.IdEstado).HasDatabaseName("IX_UsuarioTenant_Estado");
    }
}
