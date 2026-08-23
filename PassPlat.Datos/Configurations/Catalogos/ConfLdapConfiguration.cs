using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ConfLdapConfiguration : IEntityTypeConfiguration<ConfLdap>
{
    public void Configure(EntityTypeBuilder<ConfLdap> builder)
    {
        builder.ToTable("ConfLdap");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.IdTenant).IsRequired();
        builder.Property(c => c.Servidor).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Puerto).HasDefaultValue(389).IsRequired();
        builder.Property(c => c.BaseDN).HasMaxLength(500).IsRequired();
        builder.Property(c => c.BindDN).HasMaxLength(500);
        builder.Property(c => c.BindPassword).HasMaxLength(1000);
        builder.Property(c => c.UsarSSL).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.UsarStartTLS).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.FiltroBusqueda).HasMaxLength(500);
        builder.Property(c => c.AtributoEmail).HasMaxLength(100);
        builder.Property(c => c.AtributoNombre).HasMaxLength(100);
        builder.Property(c => c.AtributoUid).HasMaxLength(100);
        builder.Property(c => c.AtributoGrupo).HasMaxLength(100);
        builder.Property(c => c.TimeoutSeconds).HasDefaultValue(30);
        builder.Property(c => c.AutoProvisionar).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.SincronizarGrupos).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Estado).HasDefaultValue((byte)1).IsRequired();
        builder.Property(c => c.Metadata).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(c => c.FecMod);

        builder.HasOne(c => c.Tenant)
            .WithMany(t => t.ConfLdaps)
            .HasForeignKey(c => c.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.IdTenant).IsUnique().HasDatabaseName("UK_ConfLdap_Tenant");
        builder.HasIndex(c => c.Activo).HasFilter("Activo = 1").HasDatabaseName("IX_ConfLdap_Activo");
        builder.HasIndex(c => c.Servidor).HasDatabaseName("IX_ConfLdap_Servidor");
    }
}
