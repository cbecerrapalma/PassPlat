using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ModuloConfiguration : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("Modulos", tb => tb.HasTrigger("TR_Modulos_Mod"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.Codigo).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(m => m.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Descripcion).HasMaxLength(200);
        builder.Property(m => m.Ruta).HasMaxLength(200);
        builder.Property(m => m.Icono).HasMaxLength(50);
        builder.Property(m => m.Orden).HasColumnType("smallint").HasDefaultValue(0);
        builder.Property(m => m.EsVisibleMenu).HasDefaultValue(true).IsRequired();
        builder.Property(m => m.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(m => m.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(m => m.FecMod);

        builder.HasOne(m => m.ModuloPadre).WithMany(m => m.SubModulos).HasForeignKey(m => m.IdModuloPadre)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.TipoModulo).WithMany(t => t.Modulos).HasForeignKey(m => m.IdTipoModulo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.Codigo).IsUnique().HasDatabaseName("UQ_Modulos_Codigo");
    }
}
