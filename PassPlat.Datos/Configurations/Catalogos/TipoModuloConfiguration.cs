using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipoModuloConfiguration : IEntityTypeConfiguration<TipoModulo>
{
    public void Configure(EntityTypeBuilder<TipoModulo> builder)
    {
        builder.ToTable("TiposModulo");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(t => t.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200);
        builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
        builder.HasIndex(t => t.Codigo).IsUnique().HasDatabaseName("UQ_TiposModulo_Codigo");
    }
}
