using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipoMFAConfiguration : IEntityTypeConfiguration<TipoMFA>
{
    public void Configure(EntityTypeBuilder<TipoMFA> builder)
    {
        builder.ToTable("TiposMFA");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200);
        builder.Property(t => t.Prioridad).HasDefaultValue((byte)0).IsRequired();
        builder.Property(t => t.ReqConfig).HasDefaultValue(false).IsRequired();
        builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(t => t.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
    }
}
