using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class BloqueoConfiguration : IEntityTypeConfiguration<Bloqueo>
{
    public void Configure(EntityTypeBuilder<Bloqueo> builder)
    {
        builder.ToTable("Bloqueos");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).ValueGeneratedOnAdd();
        builder.Property(b => b.IdUsuario).IsRequired();
        builder.Property(b => b.IdTenant).IsRequired();
        builder.Property(b => b.IdTipoBloqueo).IsRequired();
        builder.Property(b => b.FecInicio).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(b => b.FecFin);
        builder.Property(b => b.Motivo).HasMaxLength(200).IsRequired();
        builder.Property(b => b.CodDesbloqueo).HasMaxLength(100);
        builder.Property(b => b.IntentosGenerados);
        builder.Property(b => b.TipoDeteccion).HasMaxLength(50);
        builder.Property(b => b.Activo).HasDefaultValue(true).IsRequired();

        builder.HasOne(b => b.Usuario)
            .WithMany(u => u.Bloqueos)
            .HasForeignKey(b => b.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Tenant)
            .WithMany(t => t.Bloqueos)
            .HasForeignKey(b => b.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.TipoBloqueo)
            .WithMany(t => t.Bloqueos)
            .HasForeignKey(b => b.IdTipoBloqueo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Agente)
            .WithMany(a => a.Bloqueos)
            .HasForeignKey(b => b.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.DireccionIP)
            .WithMany(d => d.Bloqueos)
            .HasForeignKey(b => b.IdIP)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.UsrBloqueador)
            .WithMany(u => u.BloqueosRealizados)
            .HasForeignKey(b => b.IdUsrBloqueador)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.IdUsuario, b.Activo }).HasFilter("Activo = 1").HasDatabaseName("IX_Bloqueos_Activo");
    }
}
