using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TipoAuditoriaConfiguration : IEntityTypeConfiguration<TipoAuditoria>
{
    public void Configure(EntityTypeBuilder<TipoAuditoria> builder)
    {
        builder.ToTable("TiposAuditoria");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(200);
        builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(t => t.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
    }
}
