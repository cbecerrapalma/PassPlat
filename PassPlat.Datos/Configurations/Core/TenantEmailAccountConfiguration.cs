using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class TenantEmailAccountConfiguration : IEntityTypeConfiguration<TenantEmailAccount>
{
    public void Configure(EntityTypeBuilder<TenantEmailAccount> builder)
    {
        builder.ToTable("TenantEmailAccounts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdTenant).IsRequired();
        builder.Property(e => e.IdEmailAccount).IsRequired();
        builder.Property(e => e.EsPredeterminada).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.TenantEmailAccounts)
            .HasForeignKey(e => e.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmailAccount)
            .WithMany(ea => ea.TenantEmailAccounts)
            .HasForeignKey(e => e.IdEmailAccount)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IdTenant, e.IdEmailAccount }).IsUnique().HasDatabaseName("UQ_TenantEmailAcct_TenantCuenta");
        builder.HasIndex(e => new { e.IdTenant, e.Activo }).HasDatabaseName("IX_TenantEmailAcct_Tenant");
        builder.HasIndex(e => new { e.IdEmailAccount, e.Activo }).HasDatabaseName("IX_TenantEmailAcct_Account");
        builder.HasIndex(e => e.IdTenant).IsUnique().HasFilter("EsPredeterminada = 1 AND Activo = 1").HasDatabaseName("UX_TenantEmailAcct_Predet");
    }
}
