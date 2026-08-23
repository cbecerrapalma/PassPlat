using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class AppModuloConfiguration : IEntityTypeConfiguration<AppModulo>
{
    public void Configure(EntityTypeBuilder<AppModulo> builder)
    {
        builder.ToTable("AppsModulos");
        builder.HasKey(am => am.Id);
        builder.Property(am => am.Id).ValueGeneratedOnAdd();
        builder.Property(am => am.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(am => am.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(am => am.App).WithMany(a => a.AppsModulos).HasForeignKey(am => am.IdApp)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(am => am.Modulo).WithMany(m => m.AppsModulos).HasForeignKey(am => am.IdModulo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(am => new { am.IdApp, am.IdModulo }).IsUnique().HasFilter("Activo = 1")
            .HasDatabaseName("UX_AppsModulos_Activo");
    }
}
