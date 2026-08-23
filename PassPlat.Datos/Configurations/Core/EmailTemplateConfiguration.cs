using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates", t =>
        {
            t.HasCheckConstraint("CK_EmailTpl_Categoria", "Categoria IN ('transaccional', 'marketing', 'alerta', 'sistema')");
            t.HasCheckConstraint("CK_EmailTpl_Estado", "Estado IN ('borrador', 'publicado', 'desactivado')");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdTenant);
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(e => e.Cultura).HasMaxLength(10).IsRequired().IsUnicode(false).HasDefaultValue("es");
        builder.Property(e => e.Asunto).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CuerpoHtml).HasMaxLength(-1).IsRequired();
        builder.Property(e => e.CuerpoTexto).HasMaxLength(-1);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Categoria).HasMaxLength(50).IsRequired().IsUnicode(false).HasDefaultValue("transaccional");
        builder.Property(e => e.Estado).HasMaxLength(20).IsRequired().IsUnicode(false).HasDefaultValue("borrador");
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.VariablesDoc).HasMaxLength(1000);
        builder.Property(e => e.IdUsrMod);
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(e => e.FecMod);

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.EmailTemplates)
            .HasForeignKey(e => e.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Historial)
            .WithOne(h => h.Template)
            .HasForeignKey(h => h.IdTemplate)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Nombre, e.Cultura }).IsUnique().HasDatabaseName("UQ_EmailTpl_NombreCultura");
        builder.HasIndex(e => e.Estado).HasFilter("Estado IS NOT NULL").HasDatabaseName("IX_EmailTpl_Estado");
        builder.HasIndex(e => e.IdTenant).HasFilter("IdTenant IS NOT NULL").HasDatabaseName("IX_EmailTpl_Tenant");

    }
}
