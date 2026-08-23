using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class UsuarioPermisoConfiguration : IEntityTypeConfiguration<UsuarioPermiso>
{
    public void Configure(EntityTypeBuilder<UsuarioPermiso> builder)
    {
        builder.ToTable("UsuariosPermisos", tb => { tb.HasTrigger("TR_UsuariosPermisos_ValidarTenant"); tb.HasCheckConstraint("CK_UsuariosPermisos_Fechas", "FecFin IS NULL OR FecInicio IS NULL OR FecFin > FecInicio"); });
        builder.HasKey(up => up.Id);

        builder.Property(up => up.Id).ValueGeneratedOnAdd();
        builder.Property(up => up.IdUsuario).IsRequired();
        builder.Property(up => up.IdPermiso).IsRequired();
        builder.Property(up => up.IdTenant).IsRequired();
        builder.Property(up => up.IdApp);
        builder.Property(up => up.IdTipoAsig).HasColumnType("tinyint").HasDefaultValue((byte)1).IsRequired();
        builder.Property(up => up.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(up => up.FecInicio);
        builder.Property(up => up.FecFin);
        builder.Property(up => up.Motivo).HasMaxLength(200);
        builder.Property(up => up.IdUsrCrea).IsRequired();
        builder.Property(up => up.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(up => up.IdUsrMod).IsRequired();
        builder.Property(up => up.FecMod);

        builder.HasOne(up => up.Usuario)
            .WithMany(u => u.UsuariosPermisos)
            .HasForeignKey(up => up.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.Permiso)
            .WithMany()
            .HasForeignKey(up => up.IdPermiso)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.App)
            .WithMany()
            .HasForeignKey(up => up.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.TipAsigPermiso)
            .WithMany(t => t.UsuariosPermisos)
            .HasForeignKey(up => up.IdTipoAsig)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.Tenant)
            .WithMany()
            .HasForeignKey(up => up.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.UsrCrea)
            .WithMany(u => u.UsuariosPermisosCreados)
            .HasForeignKey(up => up.IdUsrCrea)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(up => up.UsrMod)
            .WithMany(u => u.UsuariosPermisosModificados)
            .HasForeignKey(up => up.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(tb => tb.HasCheckConstraint("CK_UsuariosPermisos_Fechas", "FecFin IS NULL OR FecInicio IS NULL OR FecFin > FecInicio"));
    }
}
