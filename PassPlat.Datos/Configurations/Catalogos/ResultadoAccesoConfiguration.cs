using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ResultadoAccesoConfiguration : IEntityTypeConfiguration<ResultadoAcceso>
{
    public void Configure(EntityTypeBuilder<ResultadoAcceso> builder)
    {
        builder.ToTable("ResultadosAcceso");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Descripcion).HasMaxLength(200);
        builder.Property(r => r.EsExitoso).HasDefaultValue(false).IsRequired();
    }
}
