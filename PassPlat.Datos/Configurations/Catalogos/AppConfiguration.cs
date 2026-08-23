using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class AppConfiguration : IEntityTypeConfiguration<App>
{
    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.ToTable("Apps");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.Codigo).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(a => a.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(a => a.UrlBase).HasMaxLength(255);
        builder.Property(a => a.Activa).HasDefaultValue(true).IsRequired();
        builder.Property(a => a.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasIndex(a => a.Codigo).IsUnique();
    }
}
