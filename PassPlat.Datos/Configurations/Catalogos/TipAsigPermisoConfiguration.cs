using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipAsigPermisoConfiguration : IEntityTypeConfiguration<TipAsigPermiso>
{
    public void Configure(EntityTypeBuilder<TipAsigPermiso> builder)
    {
        builder.ToTable("TipAsigPermiso");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnType("tinyint").ValueGeneratedNever();
        builder.Property(t => t.Nombre).HasMaxLength(50).IsRequired().IsUnicode(false);
    }
}
