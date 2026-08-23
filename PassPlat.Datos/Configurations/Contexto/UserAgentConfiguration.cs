using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Contexto;

namespace PassPlat.Datos.Configurations.Contexto;

public class UserAgentConfiguration : IEntityTypeConfiguration<UserAgent>
{
    public void Configure(EntityTypeBuilder<UserAgent> builder)
    {
        builder.ToTable("UserAgents");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).ValueGeneratedOnAdd();
        builder.Property(u => u.Agente).HasMaxLength(500).IsRequired();
        builder.Property(u => u.HashAgente).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Navegador).HasMaxLength(100);
        builder.Property(u => u.Version).HasMaxLength(50);
        builder.Property(u => u.SistemaOperativo).HasMaxLength(100);
        builder.Property(u => u.EsMovil).HasDefaultValue(false);
        builder.Property(u => u.FecPrimerUso).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(u => u.FecUltUso);
        builder.Property(u => u.VecesUsado).HasDefaultValue(1).IsRequired();

        builder.HasIndex(u => u.HashAgente).IsUnique();
        builder.HasIndex(u => u.Agente).HasDatabaseName("IX_UserAgents_Agente");
    }
}
