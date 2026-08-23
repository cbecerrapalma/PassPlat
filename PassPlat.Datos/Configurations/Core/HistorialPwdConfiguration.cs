using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class HistorialPwdConfiguration : IEntityTypeConfiguration<HistorialPwd>
{
    public void Configure(EntityTypeBuilder<HistorialPwd> builder)
    {
        builder.ToTable("HistorialPwd", t =>
        {
            t.HasCheckConstraint("CK_HistorialPwd_Fortaleza",
                "Fortaleza IS NULL OR (Fortaleza BETWEEN 0 AND 100)");
            t.HasCheckConstraint("CK_HistorialPwd_Complejidad",
                "Complejidad IS NULL OR (Complejidad BETWEEN 1 AND 5)");
        });
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Property(h => h.IdUsuario).IsRequired();
        builder.Property(h => h.IdPolitica).IsRequired();
        builder.Property(h => h.HashPwd).HasMaxLength(512).IsRequired();
        builder.Property(h => h.Algoritmo).HasMaxLength(50).HasDefaultValue("Argon2id").IsRequired();
        builder.Property(h => h.ParametrosAlgoritmo).HasMaxLength(255);
        builder.Property(h => h.PepperVersion).HasDefaultValue((byte)1).IsRequired();
        builder.Property(h => h.EsActual).HasDefaultValue(false).IsRequired();
        builder.Property(h => h.EsForzado).HasDefaultValue(false).IsRequired();
        builder.Property(h => h.EsComprometida).HasDefaultValue(false).IsRequired();
        builder.Property(h => h.FecRegistro).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(h => h.FecExpira);
        builder.Property(h => h.Complejidad);
        builder.Property(h => h.Fortaleza).HasPrecision(5, 2);
        builder.Property(h => h.OrigenRegistro).HasMaxLength(20).HasDefaultValue("LOCAL").IsRequired();

        builder.Property(h => h.AnioMes)
            .HasComputedColumnSql("datepart(year,[FecRegistro])*(100)+datepart(month,[FecRegistro])", stored: true);

        builder.Property(h => h.FecRetencion)
            .HasComputedColumnSql("DATEADD(YEAR, 1, FecRegistro)", stored: true);

        builder.HasOne(h => h.Usuario)
            .WithMany(u => u.HistorialPwd)
            .HasForeignKey(h => h.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Politica)
            .WithMany(p => p.HistorialPwd)
            .HasForeignKey(h => h.IdPolitica)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Disp)
            .WithMany(d => d.HistorialPwd)
            .HasForeignKey(h => h.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.TipoCambio)
            .WithMany(t => t.HistorialPwd)
            .HasForeignKey(h => h.IdTipoCambio)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.IdUsuario).HasFilter("EsActual = 1").IsUnique().HasDatabaseName("UX_Historial_Actual");
        builder.HasIndex(h => new { h.IdUsuario, h.FecRegistro }).IsDescending(false, true).HasDatabaseName("IX_Historial_UsrFec");
        builder.HasIndex(h => h.IdPolitica).HasDatabaseName("IX_Historial_Politica");
        builder.HasIndex(h => h.FecRetencion).HasDatabaseName("IX_Historial_Ret");

    }
}
