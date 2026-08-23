using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class OutboxConfiguration : IEntityTypeConfiguration<Outbox>
{
    public void Configure(EntityTypeBuilder<Outbox> builder)
    {
        builder.ToTable("Outbox");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).ValueGeneratedOnAdd();
        builder.Property(o => o.EventType).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(o => o.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(o => o.IdTenant);
        builder.Property(o => o.IdUsuario);
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired().IsUnicode(false).HasDefaultValue("pending");
        builder.Property(o => o.Attempts).HasDefaultValue(0).IsRequired();
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(o => o.ProcessingStartedAt);
        builder.Property(o => o.ProcessedAt);
        builder.Property(o => o.LastError).HasColumnType("nvarchar(max)");
        builder.Property(o => o.NextAttemptAt);

        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("IX_Outbox_Pending_Status_CreatedOn");

        builder.HasIndex(o => o.ProcessingStartedAt)
            .HasDatabaseName("IX_Outbox_ProcessingStartedAt");

        builder.HasOne(o => o.Tenant)
            .WithMany()
            .HasForeignKey(o => o.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Usuario)
            .WithMany()
            .HasForeignKey(o => o.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
