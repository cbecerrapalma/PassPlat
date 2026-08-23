using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class DispConfiableConfiguration : IEntityTypeConfiguration<DispConfiable>
{
    public void Configure(EntityTypeBuilder<DispConfiable> builder)
    {
        builder.ToTable("DispConfiables");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.IdUsuario).IsRequired();
        builder.Property(d => d.IdTenant).IsRequired();
        builder.Property(d => d.IdDisp).IsRequired();
        builder.Property(d => d.Nombre).HasMaxLength(100);
        builder.Property(d => d.FecAlta).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(d => d.Confiable).HasDefaultValue(false).IsRequired();
        builder.Property(d => d.UltUso);

        builder.HasOne(d => d.Usuario)
            .WithMany(u => u.DispConfiables)
            .HasForeignKey(d => d.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Tenant)
            .WithMany(t => t.DispConfiables)
            .HasForeignKey(d => d.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Disp)
            .WithMany(dp => dp.DispConfiables)
            .HasForeignKey(d => d.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Agente)
            .WithMany(a => a.DispConfiables)
            .HasForeignKey(d => d.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
