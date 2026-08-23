using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class SesionConfiguration : IEntityTypeConfiguration<Sesion>
{
    public void Configure(EntityTypeBuilder<Sesion> builder)
    {
        builder.ToTable("Sesiones", tb => tb.HasTrigger("TR_Sesiones_Act"));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasDefaultValueSql("newid()");
        builder.Property(s => s.IdUsuario).IsRequired();
        builder.Property(s => s.IdTenant).IsRequired();
        builder.Property(s => s.IdApp).IsRequired();
        builder.Property(s => s.IdTokenExt).HasMaxLength(128).IsRequired();
        builder.Property(s => s.HashRefresh).HasMaxLength(128);
        builder.Property(s => s.FecInicio).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(s => s.UltActividad).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(s => s.FecExpira).IsRequired();
        builder.Property(s => s.EsActiva).HasDefaultValue(true).IsRequired();

        builder.HasOne(s => s.Usuario)
            .WithMany(u => u.Sesiones)
            .HasForeignKey(s => s.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Tenant)
            .WithMany(t => t.Sesiones)
            .HasForeignKey(s => s.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.App)
            .WithMany(a => a.Sesiones)
            .HasForeignKey(s => s.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Disp)
            .WithMany(d => d.Sesiones)
            .HasForeignKey(s => s.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.DireccionIP)
            .WithMany(d => d.Sesiones)
            .HasForeignKey(s => s.IdIP)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SesionPadre)
            .WithMany(s => s.SesionesHijas)
            .HasForeignKey(s => s.IdSesionPadre)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.IdTokenExt).IsUnique();
        builder.HasIndex(s => new { s.IdUsuario, s.IdTenant, s.IdApp, s.EsActiva }).HasDatabaseName("IX_Sesiones_Contexto");
        builder.HasIndex(s => s.FecExpira).HasFilter("EsActiva = 1").HasDatabaseName("IX_Sesiones_Expira");
        builder.HasIndex(s => s.IdSesionPadre).HasDatabaseName("IX_Sesiones_Padre");
        builder.HasIndex(s => s.IdDisp).HasDatabaseName("IX_Sesiones_Disp");
        builder.HasIndex(s => s.HashRefresh).HasFilter("HashRefresh IS NOT NULL AND EsActiva = 1").HasDatabaseName("IX_Sesiones_Refresh");
    }
}
