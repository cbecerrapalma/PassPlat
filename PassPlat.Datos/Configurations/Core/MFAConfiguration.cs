using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class MFAConfiguration : IEntityTypeConfiguration<MFA>
{
    public void Configure(EntityTypeBuilder<MFA> builder)
    {
        builder.ToTable("MFA");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.IdUsuario).IsRequired();
        builder.Property(m => m.IdTenant).IsRequired();
        builder.Property(m => m.IdTipoMFA).IsRequired();
        builder.Property(m => m.IdMFA).HasMaxLength(200).IsRequired();
        builder.Property(m => m.ClavePublica).HasMaxLength(500);
        builder.Property(m => m.EsPrincipal).HasDefaultValue(false).IsRequired();
        builder.Property(m => m.FecAlta).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(m => m.IdEstado).IsRequired();
        builder.Property(m => m.Metadatos).HasColumnType("nvarchar(max)");
        builder.Property(m => m.UltUso);

        builder.HasOne(m => m.Usuario)
            .WithMany(u => u.MFA)
            .HasForeignKey(m => m.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.MFA)
            .HasForeignKey(m => m.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.TipoMFA)
            .WithMany(t => t.MFA)
            .HasForeignKey(m => m.IdTipoMFA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Estado)
            .WithMany(e => e.MFA)
            .HasForeignKey(m => m.IdEstado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.IdUsuario).HasFilter("EsPrincipal = 1").IsUnique().HasDatabaseName("UX_MFA_Principal");
    }
}
