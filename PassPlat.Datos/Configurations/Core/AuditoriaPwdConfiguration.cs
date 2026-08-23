using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class AuditoriaPwdConfiguration : IEntityTypeConfiguration<AuditoriaPwd>
{
    public void Configure(EntityTypeBuilder<AuditoriaPwd> builder)
    {
        builder.ToTable("AuditoriaPwd");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.IdUsuario).IsRequired();
        builder.Property(a => a.IdTipoAccion).IsRequired();
        builder.Property(a => a.NivelRiesgo);
        builder.Property(a => a.Detalles).HasMaxLength(500);
        builder.Property(a => a.Metadata).HasMaxLength(-1);
        builder.Property(a => a.FecAccion).HasDefaultValueSql("sysutcdatetime()");

        builder.Property(a => a.FecRetencion)
            .HasComputedColumnSql("DATEADD(YEAR, 1, FecAccion)", stored: true);

        builder.HasOne(a => a.Usuario)
            .WithMany(u => u.AuditoriaPwd)
            .HasForeignKey(a => a.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.AuditoriaPwd)
            .HasForeignKey(a => a.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.App)
            .WithMany(a => a.AuditoriaPwd)
            .HasForeignKey(a => a.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.TipoAccion)
            .WithMany(t => t.AuditoriaPwd)
            .HasForeignKey(a => a.IdTipoAccion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.UsrEjecutor)
            .WithMany(u => u.AuditoriaEjecutada)
            .HasForeignKey(a => a.IdUsrEjecutor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Disp)
            .WithMany(d => d.AuditoriaPwd)
            .HasForeignKey(a => a.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Agente)
            .WithMany(a => a.AuditoriaPwd)
            .HasForeignKey(a => a.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.DireccionIP)
            .WithMany(d => d.AuditoriaPwd)
            .HasForeignKey(a => a.IdIP)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.HistPwd)
            .WithMany(h => h.AuditoriaPwd)
            .HasForeignKey(a => a.IdHistPwd)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.IdUsuario, a.FecAccion }).IsDescending(false, true).HasDatabaseName("IX_Auditoria_UsrFec");
        builder.HasIndex(a => new { a.IdTipoAccion, a.FecAccion }).IsDescending(false, true).HasDatabaseName("IX_Auditoria_Tipo");
        builder.HasIndex(a => a.FecRetencion).HasDatabaseName("IX_Auditoria_Ret");
    }
}
