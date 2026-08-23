using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios", tb =>
        {
            tb.HasTrigger("TR_Usuarios_Mod");
            tb.HasTrigger("TR_Usuarios_NoDeleteSistema");
            tb.HasTrigger("TR_Usuarios_NoDesactivarSistema");
            tb.HasTrigger("TR_Usuarios_ValidarEsSistema");
        });
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).ValueGeneratedOnAdd();
        builder.Property(u => u.IdTenant).IsRequired();
        builder.Property(u => u.IdEstado).IsRequired();
        builder.Property(u => u.NomUsuario).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired(false);
        builder.Property(u => u.EmailVerificado).HasDefaultValue(false).IsRequired();
        builder.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(u => u.ReqCambioPwd).HasDefaultValue(true).IsRequired();
        builder.Property(u => u.IntentosFallidos).HasDefaultValue((byte)0).IsRequired();
        builder.Property(u => u.EsSistema).HasDefaultValue(false).IsRequired();
        builder.Property(u => u.TienePasswordLocal).HasDefaultValue(false).IsRequired();
        builder.Property(u => u.Eliminado).HasDefaultValue(false).IsRequired();
        builder.Property(u => u.FecUltIntentoFallido);
        builder.Property(u => u.FecUltCambioPwd);
        builder.Property(u => u.FecVerifBrecha);
        builder.Property(u => u.FecEliminacion);
        builder.Property(u => u.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(u => u.FecMod).HasDefaultValueSql("sysutcdatetime()");

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.Usuarios)
            .HasForeignKey(u => u.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Estado)
            .WithMany(e => e.Usuarios)
            .HasForeignKey(u => u.IdEstado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.IdTenant, u.NomUsuario }).IsUnique().HasFilter("Eliminado = 0").HasDatabaseName("UX_Usuarios_TenantNomUsuario");
        builder.HasIndex(u => new { u.IdTenant, u.Email }).IsUnique().HasFilter("Eliminado = 0 AND Email IS NOT NULL").HasDatabaseName("UX_Usuarios_TenantEmail");
        builder.HasIndex(u => u.Eliminado).HasFilter("Eliminado = 1").HasDatabaseName("IX_Usuarios_Eliminados");
    }
}
