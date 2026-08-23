using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipoCambioPwdConfiguration : IEntityTypeConfiguration<TipoCambioPwd>
{
    public void Configure(EntityTypeBuilder<TipoCambioPwd> builder)
    {
        builder.ToTable("TiposCambioPwd");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(t => t.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200);
    }
}
