using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipoDispConfiguration : IEntityTypeConfiguration<TipoDisp>
{
    public void Configure(EntityTypeBuilder<TipoDisp> builder)
    {
        builder.ToTable("TiposDisp");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200);
        builder.Property(t => t.EsMovil).HasDefaultValue(false).IsRequired();
    }
}
