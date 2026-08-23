using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Contexto;

namespace PassPlat.Datos.Configurations.Contexto;

public class IPConfiguration : IEntityTypeConfiguration<IP>
{
    public void Configure(EntityTypeBuilder<IP> builder)
    {
        builder.ToTable("IPs");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.Direccion).HasMaxLength(45).IsRequired();
        builder.Property(d => d.TipoIP).IsRequired();
        builder.Property(d => d.Pais).HasMaxLength(100);
        builder.Property(d => d.Ciudad).HasMaxLength(100);
        builder.Property(d => d.EsSospechosa).HasDefaultValue(false).IsRequired();
        builder.Property(d => d.FecPrimerUso).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(d => d.UltUso);

        builder.HasIndex(d => d.Direccion).IsUnique();
    }
}
