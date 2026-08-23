using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class EmailTemplateHistorialConfiguration : IEntityTypeConfiguration<EmailTemplateHistorial>
{
    public void Configure(EntityTypeBuilder<EmailTemplateHistorial> builder)
    {
        builder.ToTable("EmailTemplateHistorial");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdTemplate).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Asunto).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CuerpoHtml).HasMaxLength(-1).IsRequired();
        builder.Property(e => e.CuerpoTexto).HasMaxLength(-1);
        builder.Property(e => e.FecPublicacion).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(e => e.IdUsrPublico).IsRequired();
        builder.Property(e => e.Motivo).HasMaxLength(500);

        builder.HasOne(e => e.Template)
            .WithMany(t => t.Historial)
            .HasForeignKey(e => e.IdTemplate)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.IdUsrPublico)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IdTemplate, e.Version }).IsDescending(false, true).HasDatabaseName("IX_EmailTplHist_Template");
    }
}
