using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class AudIdenExtConfiguration : IEntityTypeConfiguration<AudIdenExt>
{
    public void Configure(EntityTypeBuilder<AudIdenExt> builder)
    {
        builder.ToTable("AudIdenExt");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        builder.Property(a => a.IdTenant).IsRequired();
        builder.Property(a => a.IdProvIden).IsRequired();
        builder.Property(a => a.IdUsuario);
        builder.Property(a => a.SubExterno).HasMaxLength(255);
        builder.Property(a => a.Evento).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Resultado).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Detalle).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IP).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.CorrelationId).HasMaxLength(50);
        builder.Property(a => a.FecEvento).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.Property(a => a.TraceId).HasMaxLength(100);
        builder.Property(a => a.SessionId);
        builder.Property(a => a.RefreshTokenId).HasMaxLength(500);
        builder.Property(a => a.JwtId).HasMaxLength(500);
        builder.Property(a => a.HttpStatus);
        builder.Property(a => a.TiempoRespuesta);
        builder.Property(a => a.Scopes).HasMaxLength(2000);
        builder.Property(a => a.MetodoAutenticacion).HasMaxLength(50);
        builder.Property(a => a.TipoLogin).HasMaxLength(50);
        builder.Property(a => a.Origen).HasMaxLength(50);
        builder.Property(a => a.Destino).HasMaxLength(500);
        builder.Property(a => a.Codigo).HasMaxLength(100);
        builder.Property(a => a.Excepcion).HasColumnType("nvarchar(max)");
        builder.Property(a => a.StackResumido).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IdDevice);
        builder.Property(a => a.Browser).HasMaxLength(200);
        builder.Property(a => a.OS).HasMaxLength(200);

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.AuditoriasIdenExt)
            .HasForeignKey(a => a.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ProvIden)
            .WithMany(p => p.Auditorias)
            .HasForeignKey(a => a.IdProvIden)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Usuario)
            .WithMany(u => u.AuditoriasIdenExt)
            .HasForeignKey(a => a.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Device)
            .WithMany()
            .HasForeignKey(a => a.IdDevice)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.IdTenant, a.FecEvento }).IsDescending(false, true).HasDatabaseName("IX_AudIdenExt_TenantFecha");
        builder.HasIndex(a => new { a.IdProvIden, a.FecEvento }).IsDescending(false, true).HasDatabaseName("IX_AudIdenExt_ProvIden");
        builder.HasIndex(a => a.CorrelationId).HasDatabaseName("IX_AudIdenExt_CorrelationId");
    }
}
