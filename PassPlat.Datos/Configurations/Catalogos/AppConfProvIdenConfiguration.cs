using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

// S126 · D3 — en SQL las FK no tienen cascada; ClientCascade borra el enlace que se quita de ConfProvIden.Apps
public class AppConfProvIdenConfiguration : IEntityTypeConfiguration<AppConfProvIden>
{
    public void Configure(EntityTypeBuilder<AppConfProvIden> builder)
    {
        builder.ToTable("AppsConfProvIden");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.FecCrea).HasDefaultValueSql("sysdatetime()").IsRequired();

        builder.HasOne(x => x.App).WithMany().HasForeignKey(x => x.IdApp)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConfProvIden).WithMany(c => c.Apps).HasForeignKey(x => x.IdConfProvIden)
            .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasIndex(x => new { x.IdConfProvIden, x.IdApp }).IsUnique()
            .HasDatabaseName("UQ_AppsConfProvIden_ConfApp");
    }
}
