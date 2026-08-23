using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class EstadoUsrConfiguration : IEntityTypeConfiguration<EstadoUsr>
{
    public void Configure(EntityTypeBuilder<EstadoUsr> builder)
    {
        builder.ToTable("EstadosUsr");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(e => e.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(200);
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
    }
}
