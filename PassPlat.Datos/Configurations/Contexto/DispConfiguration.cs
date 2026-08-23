using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Contexto;

namespace PassPlat.Datos.Configurations.Contexto;

public class DispConfiguration : IEntityTypeConfiguration<Disp>
{
    public void Configure(EntityTypeBuilder<Disp> builder)
    {
        builder.ToTable("Disp");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.IdTipoDisp).IsRequired();
        builder.Property(d => d.Fabricante).HasMaxLength(100);
        builder.Property(d => d.Modelo).HasMaxLength(100);
        builder.Property(d => d.FecPrimerReg).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(d => d.UltActividad);
        builder.Property(d => d.CantidadLogins).HasDefaultValue(0).IsRequired();
        builder.Property(d => d.IP).HasMaxLength(45);
        builder.Property(d => d.Pais).HasMaxLength(100);
        builder.Property(d => d.Navegador).HasMaxLength(255);
        builder.Property(d => d.SO).HasMaxLength(255);
        builder.Property(d => d.ProveedorAuth).HasMaxLength(50);

        builder.HasOne(d => d.TipoDisp)
            .WithMany(t => t.Disp)
            .HasForeignKey(d => d.IdTipoDisp)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
