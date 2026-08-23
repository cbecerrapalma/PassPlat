using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class TokenRestConfiguration : IEntityTypeConfiguration<TokenRest>
{
    public void Configure(EntityTypeBuilder<TokenRest> builder)
    {
        builder.ToTable("TokensRest");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.IdUsuario).IsRequired();
        builder.Property(t => t.IdTenant).IsRequired();
        builder.Property(t => t.HashToken).HasMaxLength(255).IsRequired();
        builder.Property(t => t.FecGeneracion).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(t => t.FecVence).IsRequired();
        builder.Property(t => t.EsUtilizado).HasDefaultValue(false).IsRequired();
        builder.Property(t => t.IntentosFallidos).HasDefaultValue((byte)0).IsRequired();
        builder.Property(t => t.FecUso);

        builder.HasOne(t => t.Usuario)
            .WithMany(u => u.TokensRest)
            .HasForeignKey(t => t.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Tenant)
            .WithMany(t => t.TokensRest)
            .HasForeignKey(t => t.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.App)
            .WithMany(a => a.TokensRest)
            .HasForeignKey(t => t.IdApp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Disp)
            .WithMany(d => d.TokensRest)
            .HasForeignKey(t => t.IdDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Agente)
            .WithMany(a => a.TokensRest)
            .HasForeignKey(t => t.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.DireccionIP)
            .WithMany(d => d.TokensRest)
            .HasForeignKey(t => t.IdIP)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.HashToken).HasFilter("EsUtilizado = 0").IsUnique().HasDatabaseName("UX_Tokens_Hash");
        builder.HasIndex(t => new { t.IdUsuario, t.IdApp, t.EsUtilizado }).HasDatabaseName("IX_Tokens_UsrApp");
        builder.HasIndex(t => t.FecVence).HasDatabaseName("IX_Tokens_Vence");
    }
}
