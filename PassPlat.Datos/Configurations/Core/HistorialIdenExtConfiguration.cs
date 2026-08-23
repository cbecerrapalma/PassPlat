using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class HistorialIdenExtConfiguration : IEntityTypeConfiguration<HistorialIdenExt>
{
    public void Configure(EntityTypeBuilder<HistorialIdenExt> builder)
    {
        builder.ToTable("HistorialIdenExt");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();

        builder.Property(h => h.IdTenant).IsRequired();
        builder.Property(h => h.IdUsuario).IsRequired();
        builder.Property(h => h.IdIdenExt).IsRequired().HasColumnName("IdIdentidadExterna");
        builder.Property(h => h.IdProvIden).IsRequired();
        builder.Property(h => h.TipoCambio).HasMaxLength(100).IsRequired();
        builder.Property(h => h.ValorAnterior).HasMaxLength(1000);
        builder.Property(h => h.ValorNuevo).HasMaxLength(1000);
        builder.Property(h => h.EsAutomatico).HasDefaultValue(false).IsRequired();
        builder.Property(h => h.CorrelationId);
        builder.Property(h => h.FecCambio).HasDefaultValueSql("sysutcdatetime()");

        builder.HasOne(h => h.Tenant)
            .WithMany()
            .HasForeignKey(h => h.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Usuario)
            .WithMany()
            .HasForeignKey(h => h.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.IdenExt)
            .WithMany()
            .HasForeignKey(h => h.IdIdenExt)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ProvIden)
            .WithMany()
            .HasForeignKey(h => h.IdProvIden)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.RealizadoPorNav)
            .WithMany()
            .HasForeignKey(h => h.RealizadoPor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.IdUsuario).HasDatabaseName("IX_HistorialIdEx_Usuario");
        builder.HasIndex(h => h.IdIdenExt).HasDatabaseName("IX_HistorialIdEx_Identidad");
        builder.HasIndex(h => h.FecCambio).IsDescending(true).HasDatabaseName("IX_HistorialIdEx_FecCambio");
        builder.HasIndex(h => h.IdTenant).HasDatabaseName("IX_HistorialIdEx_Tenant");
    }
}
