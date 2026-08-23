using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class EstIdenExtConfiguration : IEntityTypeConfiguration<EstIdenExt>
{
    public void Configure(EntityTypeBuilder<EstIdenExt> builder)
    {
        builder.ToTable("EstIdenExt");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Color).HasMaxLength(20);
        builder.Property(e => e.Orden).HasDefaultValue((short)0);
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(e => e.FecMod);

        builder.HasIndex(e => e.Nombre).IsUnique().HasDatabaseName("UK_EstIdenExt_Nombre");
    }
}
