using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class IntentoAccesoConfiguration : IEntityTypeConfiguration<IntentoAcceso>
{
    public void Configure(EntityTypeBuilder<IntentoAcceso> builder)
    {
        builder.ToTable("IntentosAcceso");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Property(i => i.IdResultado).IsRequired();
        builder.Property(i => i.NomUsuarioIntentado).HasMaxLength(100).IsRequired();
        builder.Property(i => i.MetodoAutenticacion).HasMaxLength(20).HasDefaultValue("Local").IsRequired();
        builder.Property(i => i.FecIntento).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(i => i.Exitoso).HasDefaultValue(false).IsRequired();
        builder.Property(i => i.DetResultado).HasMaxLength(200);
        builder.Property(i => i.TpoRespuesta);
        builder.Property(i => i.CodRespuesta);

        builder.Property(i => i.FecRetencion)
            .HasComputedColumnSql("DATEADD(YEAR, 1, FecIntento)", stored: true);

        builder.HasOne(i => i.Usuario)
            .WithMany(u => u.IntentosAcceso)
            .HasForeignKey(i => i.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Tenant)
            .WithMany(t => t.IntentosAcceso)
            .HasForeignKey(i => i.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.App)
            .WithMany(a => a.IntentosAcceso)
            .HasForeignKey(i => i.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Resultado)
            .WithMany(r => r.IntentosAcceso)
            .HasForeignKey(i => i.IdResultado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Disp)
            .WithMany(d => d.IntentosAcceso)
            .HasForeignKey(i => i.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Agente)
            .WithMany(a => a.IntentosAcceso)
            .HasForeignKey(i => i.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.DireccionIP)
            .WithMany(d => d.IntentosAcceso)
            .HasForeignKey(i => i.IdIP)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.IdUsuario, i.IdApp, i.FecIntento }).IsDescending(false, false, true).HasDatabaseName("IX_Intentos_UsrApp");
        builder.HasIndex(i => i.FecIntento).IsDescending(true).HasDatabaseName("IX_Intentos_Purga");
        builder.HasIndex(i => new { i.NomUsuarioIntentado, i.FecIntento }).IsDescending(false, true).HasDatabaseName("IX_Intentos_Nom");
        builder.HasIndex(i => i.FecRetencion).HasDatabaseName("IX_Intentos_Ret");
    }
}
