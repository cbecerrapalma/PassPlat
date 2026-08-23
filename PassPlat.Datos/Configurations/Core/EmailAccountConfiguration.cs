using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class EmailAccountConfiguration : IEntityTypeConfiguration<EmailAccount>
{
    public void Configure(EntityTypeBuilder<EmailAccount> builder)
    {
        builder.ToTable("EmailAccounts", tb => tb.HasTrigger("TR_EmailAccounts_Mod"));
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.IdProvider).HasColumnType("tinyint").IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(200);
        builder.Property(e => e.Host).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Puerto).HasDefaultValue(587).IsRequired();
        builder.Property(e => e.SmtpUsuario).HasColumnName("Usuario").HasMaxLength(255).IsRequired();
        builder.Property(e => e.Password).HasMaxLength(500).IsRequired();
        builder.Property(e => e.FromAddress).HasMaxLength(255).IsRequired();
        builder.Property(e => e.FromName).HasMaxLength(255);
        builder.Property(e => e.UsaSSL).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.UsaTLS).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.EsPredeterminada).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.IdUsrMod);
        builder.Property(e => e.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(e => e.FecMod);

        builder.HasOne(e => e.Provider)
            .WithMany(p => p.EmailAccounts)
            .HasForeignKey(e => e.IdProvider)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IdProvider, e.Activo }).HasDatabaseName("IX_EmailAccounts_Provider");
        builder.HasIndex(e => e.EsPredeterminada).HasFilter("EsPredeterminada = 1 AND Activo = 1").HasDatabaseName("IX_EmailAccounts_Predet");
        builder.HasIndex(e => e.Activo).HasDatabaseName("IX_EmailAccounts_Activo");
    }
}
