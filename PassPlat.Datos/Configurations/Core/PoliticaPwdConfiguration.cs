using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class PoliticaPwdConfiguration : IEntityTypeConfiguration<PoliticaPwd>
{
    public void Configure(EntityTypeBuilder<PoliticaPwd> builder)
    {
        builder.ToTable("PoliticasPwd", t =>
        {
            t.HasCheckConstraint("CK_PoliticasPwd_Long", "LongMax > LongMin AND LongMin >= 8");
            t.HasCheckConstraint("CK_PoliticasPwd_Vig", "DiasVigencia >= 0 AND MaxIntentos >= 1");
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Version).HasDefaultValue(1).IsRequired();
        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LongMin).HasDefaultValue((byte)8).IsRequired();
        builder.Property(p => p.LongMax).HasDefaultValue((byte)64).IsRequired();
        builder.Property(p => p.ReqMayuscula).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ReqMinuscula).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ReqNumero).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ReqEspecial).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.CaracteresEspeciales).HasMaxLength(100).HasDefaultValue("!@#$%^&*()_+-=[]{}|;:,.<>?");
        builder.Property(p => p.ProhSecuenciales).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ProhRepetitivos).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ProhPatrones).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ProhPwdComun).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.ProhInfoUsuario).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.VerificarBrechas).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.PermitirEspacios).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.DiasVigencia).HasDefaultValue((short)0).IsRequired();
        builder.Property(p => p.PwdRecordadas).HasDefaultValue((byte)24).IsRequired();
        builder.Property(p => p.MaxIntentos).HasDefaultValue((byte)5).IsRequired();
        builder.Property(p => p.DurBloqueoMin).HasDefaultValue(30).IsRequired();
        builder.Property(p => p.Activa).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(p => p.FecMod).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(p => p.IdUsrMod);

        builder.HasOne(p => p.Tenant)
            .WithMany(t => t.PoliticasPwd)
            .HasForeignKey(p => p.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.App)
            .WithMany(a => a.PoliticasPwd)
            .HasForeignKey(p => p.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.UsrMod)
            .WithMany()
            .HasForeignKey(p => p.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Id).HasFilter("IdTenant IS NULL AND IdApp IS NULL AND Activa = 1").IsUnique().HasDatabaseName("UX_Politicas_Global");
        builder.HasIndex(p => p.IdTenant).HasFilter("IdApp IS NULL AND Activa = 1").IsUnique().HasDatabaseName("UX_Politicas_Tenant");
        builder.HasIndex(p => new { p.IdTenant, p.IdApp }).HasFilter("Activa = 1").IsUnique().HasDatabaseName("UX_Politicas_TenantApp");

    }
}
