using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("Permisos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(200);
        builder.Property(p => p.IdModulo).IsRequired();
        builder.Property(p => p.Orden).HasColumnType("tinyint").HasDefaultValue(0);
        builder.Property(p => p.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(p => p.Modulo).WithMany(m => m.Permisos).HasForeignKey(p => p.IdModulo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.RolesPermisos)
            .WithOne(rp => rp.Permiso)
            .HasForeignKey(rp => rp.IdPermiso)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UQ_Permisos_Codigo");
    }
}
