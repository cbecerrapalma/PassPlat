using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class LdapSyncLogConfiguration : IEntityTypeConfiguration<LdapSyncLog>
{
    public void Configure(EntityTypeBuilder<LdapSyncLog> builder)
    {
        builder.ToTable("LdapSyncLog");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.IdTenant).IsRequired();
        builder.Property(l => l.IdUsuario);
        builder.Property(l => l.Operacion).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Resultado).HasMaxLength(50).IsRequired();
        builder.Property(l => l.LdapUid).HasMaxLength(255);
        builder.Property(l => l.Detalle).HasColumnType("nvarchar(max)");
        builder.Property(l => l.UsuariosCreados);
        builder.Property(l => l.UsuariosActualizados);
        builder.Property(l => l.UsuariosDesactivados);
        builder.Property(l => l.Errores);
        builder.Property(l => l.FecOperacion).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(l => l.Tenant)
            .WithMany(t => t.LdapSyncLogs)
            .HasForeignKey(l => l.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Usuario)
            .WithMany()
            .HasForeignKey(l => l.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.IdTenant).HasDatabaseName("IX_LdapSyncLog_Tenant");
        builder.HasIndex(l => l.FecOperacion).IsDescending(true).HasDatabaseName("IX_LdapSyncLog_FecOperacion");
        builder.HasIndex(l => l.Operacion).HasDatabaseName("IX_LdapSyncLog_Operacion");
    }
}
