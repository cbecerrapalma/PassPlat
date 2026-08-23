using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class AccesoConfiguration : IEntityTypeConfiguration<Acceso>
{
    public void Configure(EntityTypeBuilder<Acceso> builder)
    {
        // EF model aligned with live schema after A1 trigger removal.
        // TR_Accesos_ValidarTenant fue eliminado en A1 (007_Triggers.sql); su
        // garantía fue reemplazada por la FK compuesta FK_Accesos_UsuarioTenant.
        builder.ToTable("Accesos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.IdUsuario).IsRequired();
        builder.Property(a => a.IdTenant).IsRequired();
        builder.Property(a => a.IdApp).IsRequired();
        builder.Property(a => a.IdRol).IsRequired();
        builder.Property(a => a.IdUsuarioTenant);
        builder.Property(a => a.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(a => a.FecAsignacion).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(a => a.Usuario)
            .WithMany(u => u.Accesos)
            .HasForeignKey(a => a.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.Accesos)
            .HasForeignKey(a => a.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.App)
            .WithMany(app => app.Accesos)
            .HasForeignKey(a => a.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Rol)
            .WithMany(r => r.Accesos)
            .HasForeignKey(a => a.IdRol)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.UsuarioTenant)
            .WithMany(ut => ut.Accesos)
            .HasForeignKey(a => new { a.IdUsuarioTenant, a.IdUsuario })
            .HasPrincipalKey(ut => new { ut.Id, ut.IdUsuario })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.IdUsuario, a.IdApp, a.IdRol }).IsUnique();
        builder.HasIndex(a => a.IdTenant).HasDatabaseName("IX_Accesos_Tenant");
        builder.HasIndex(a => a.IdApp).HasDatabaseName("IX_Accesos_App");
    }
}
