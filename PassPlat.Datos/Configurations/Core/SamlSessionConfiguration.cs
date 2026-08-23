using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class SamlSessionConfiguration : IEntityTypeConfiguration<SamlSession>
{
    public void Configure(EntityTypeBuilder<SamlSession> builder)
    {
        builder.ToTable("SamlSession");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.Property(s => s.IdTenant).IsRequired();
        builder.Property(s => s.IdUsuario);
        builder.Property(s => s.IdConfSaml).IsRequired();
        builder.Property(s => s.NameId).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SessionIndex).HasMaxLength(500);
        builder.Property(s => s.NotOnOrAfter).HasMaxLength(50);
        builder.Property(s => s.SubjectConfirmationData).HasMaxLength(500);
        builder.Property(s => s.AttributesJson).HasColumnType("nvarchar(max)");
        builder.Property(s => s.EsActiva).HasDefaultValue(true).IsRequired();
        builder.Property(s => s.FecExpira);
        builder.Property(s => s.FecCreacion).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(s => s.FecRevocacion);

        builder.HasOne(s => s.Tenant)
            .WithMany(t => t.SamlSessions)
            .HasForeignKey(s => s.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ConfSaml)
            .WithMany()
            .HasForeignKey(s => s.IdConfSaml)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.IdTenant).HasDatabaseName("IX_SamlSession_Tenant");
        builder.HasIndex(s => s.NameId).HasDatabaseName("IX_SamlSession_NameId");
        builder.HasIndex(s => s.EsActiva).HasFilter("EsActiva = 1").HasDatabaseName("IX_SamlSession_Activa");
        builder.HasIndex(s => s.FecExpira).HasFilter("FecExpira IS NOT NULL AND EsActiva = 1").HasDatabaseName("IX_SamlSession_Expira");
    }
}
