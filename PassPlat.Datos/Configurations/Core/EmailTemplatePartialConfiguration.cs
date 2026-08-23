using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class EmailTemplatePartialConfiguration : IEntityTypeConfiguration<EmailTemplatePartial>
{
    public void Configure(EntityTypeBuilder<EmailTemplatePartial> builder)
    {
        builder.ToTable("EmailTemplatePartials");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(e => e.CuerpoHtml).HasMaxLength(-1).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.IdUsrMod);
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(e => e.FecMod);

        builder.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("UQ_EmailTplPart_Nombre");
    }
}
