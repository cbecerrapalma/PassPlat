using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("EmailLog");
        builder.HasKey(el => el.Id);

        builder.Property(el => el.Id).ValueGeneratedOnAdd();
        builder.Property(el => el.IdTenant);
        builder.Property(el => el.IdUsuario);
        builder.Property(el => el.IdApp);
        builder.Property(el => el.IdTemplate);
        builder.Property(el => el.IdEmailAccount);
        builder.Property(el => el.Destinatario).HasMaxLength(255).IsRequired();
        builder.Property(el => el.Asunto).HasMaxLength(500).IsRequired();
        builder.Property(el => el.Estado).HasMaxLength(20).IsRequired().IsUnicode(false).HasDefaultValue("pendiente");
        builder.Property(el => el.Proveedor).HasMaxLength(50).IsUnicode(false);
        builder.Property(el => el.MsgIdExterno).HasMaxLength(200);
        builder.Property(el => el.Intentos).HasColumnType("tinyint").HasDefaultValue((byte)0).IsRequired();
        builder.Property(el => el.FecEnvio);
        builder.Property(el => el.FecUltIntento);
        builder.Property(el => el.ErrorDetalle).HasMaxLength(500);
        builder.Property(el => el.CorrelationId).HasMaxLength(64).IsUnicode(false);
        builder.Property(el => el.ExtraJson);
        builder.Property(el => el.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasOne(el => el.Tenant)
            .WithMany()
            .HasForeignKey(el => el.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(el => el.Usuario)
            .WithMany(u => u.EmailLogs)
            .HasForeignKey(el => el.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(el => el.App)
            .WithMany(a => a.EmailLogs)
            .HasForeignKey(el => el.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(el => el.Template)
            .WithMany()
            .HasForeignKey(el => el.IdTemplate)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(el => el.EmailAccount)
            .WithMany(ea => ea.EmailLogs)
            .HasForeignKey(el => el.IdEmailAccount)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(tb => tb.HasCheckConstraint("CK_EmailLog_Estado", "Estado IN ('pendiente','enviado','fallido','rebotado')"));
    }
}
