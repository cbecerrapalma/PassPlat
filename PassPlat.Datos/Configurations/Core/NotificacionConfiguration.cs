using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("Notificaciones");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).ValueGeneratedOnAdd();
        builder.Property(n => n.IdUsuario).IsRequired();
        builder.Property(n => n.IdTenant).IsRequired();
        builder.Property(n => n.TipoNotif).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(n => n.Asunto).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Mensaje).HasMaxLength(-1);
        builder.Property(n => n.Leida).HasDefaultValue(false).IsRequired();
        builder.Property(n => n.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(n => n.FecLeida);

        builder.HasOne(n => n.Usuario)
            .WithMany(u => u.Notificaciones)
            .HasForeignKey(n => n.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Tenant)
            .WithMany(t => t.Notificaciones)
            .HasForeignKey(n => n.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.IdUsuario, n.Leida }).HasFilter("Leida = 0").HasDatabaseName("IX_Notif_Leida");
    }
}
