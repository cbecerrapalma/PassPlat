using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class AppEmailAccountConfiguration : IEntityTypeConfiguration<AppEmailAccount>
{
    public void Configure(EntityTypeBuilder<AppEmailAccount> builder)
    {
        builder.ToTable("AppEmailAccounts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdApp).IsRequired();
        builder.Property(e => e.IdEmailAccount).IsRequired();
        builder.Property(e => e.EsPredeterminada).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(e => e.App)
            .WithMany(a => a.AppEmailAccounts)
            .HasForeignKey(e => e.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmailAccount)
            .WithMany(ea => ea.AppEmailAccounts)
            .HasForeignKey(e => e.IdEmailAccount)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IdApp, e.IdEmailAccount }).IsUnique().HasDatabaseName("UQ_AppEmailAcct_AppCuenta");
        builder.HasIndex(e => new { e.IdApp, e.Activo }).HasDatabaseName("IX_AppEmailAcct_App");
        builder.HasIndex(e => new { e.IdEmailAccount, e.Activo }).HasDatabaseName("IX_AppEmailAcct_Account");
        builder.HasIndex(e => e.IdApp).IsUnique().HasFilter("EsPredeterminada = 1 AND Activo = 1").HasDatabaseName("UX_AppEmailAcct_Predet");
    }
}
