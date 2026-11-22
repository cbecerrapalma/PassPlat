using CBP.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PassPlat.Datos.Configurations.Core;

// S136 · D6 — mapeo de la fila genérica de CBP.Outbox; sin navegaciones (la BD no tiene FK)
public class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("Outbox");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).ValueGeneratedOnAdd();
        builder.Property(o => o.EventId).HasDefaultValueSql("newid()").IsRequired();
        builder.Property(o => o.EventType).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(o => o.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(o => o.IdTenant);
        builder.Property(o => o.IdUsuario);
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired().IsUnicode(false).HasDefaultValue(OutboxEstados.Pending);
        builder.Property(o => o.Attempts).HasDefaultValue(0).IsRequired();
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("sysdatetime()").IsRequired();
        builder.Property(o => o.ProcessingStartedAt);
        builder.Property(o => o.ProcessedAt);
        builder.Property(o => o.LastError).HasColumnType("nvarchar(max)");
        builder.Property(o => o.NextAttemptAt);

        builder.HasIndex(o => o.EventId)
            .IsUnique()
            .HasDatabaseName("UX_Outbox_EventId");

        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("IX_Outbox_Pending_Status_CreatedOn");

        builder.HasIndex(o => o.ProcessingStartedAt)
            .HasDatabaseName("IX_Outbox_ProcessingStartedAt");

        builder.HasIndex(o => new { o.Status, o.NextAttemptAt })
            .HasDatabaseName("IX_Outbox_Status_NextAttemptAt");
    }
}
