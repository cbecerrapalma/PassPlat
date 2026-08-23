using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class IdenExtTokensConfiguration : IEntityTypeConfiguration<IdenExtTokens>
{
    public void Configure(EntityTypeBuilder<IdenExtTokens> builder)
    {
        builder.ToTable("IdenExtTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.IdIdenExt).IsRequired();

        builder.Property(t => t.AccessTokenEnc).HasColumnType("varbinary(4000)");
        builder.Property(t => t.AccessTokenHash).HasMaxLength(128);
        builder.Property(t => t.AccessTokenExpires);

        builder.Property(t => t.RefreshTokenEnc).HasColumnType("varbinary(4000)");
        builder.Property(t => t.RefreshTokenHash).HasMaxLength(128);
        builder.Property(t => t.RefreshTokenExpires);

        builder.Property(t => t.IdTokenEnc).HasColumnType("varbinary(8000)");
        builder.Property(t => t.IdTokenHash).HasMaxLength(128);

        builder.Property(t => t.Scope).HasMaxLength(1000);
        builder.Property(t => t.TokenType).HasMaxLength(50);
        builder.Property(t => t.CorrelationId).HasMaxLength(50);
        builder.Property(t => t.HashAlgoritmo).HasMaxLength(20).HasDefaultValue("SHA256");

        builder.Property(t => t.Version).HasDefaultValue(1);
        builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(t => t.Revocado).HasDefaultValue(false);
        builder.Property(t => t.FechaRenovacion);
        builder.Property(t => t.UltimoUso);
        builder.Property(t => t.FechaRevocacion);
        builder.Property(t => t.MotivoRevocacion).HasMaxLength(500);

        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.IdenExt)
            .WithMany()
            .HasForeignKey(t => t.IdIdenExt)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.IdIdenExt).HasDatabaseName("IX_IdenExtTokens_IdIdenExt");
        builder.HasIndex(t => t.Activo).HasFilter("Activo = 1").HasDatabaseName("IX_IdenExtTokens_Activos");
        builder.HasIndex(t => t.RefreshTokenHash).HasFilter("RefreshTokenHash IS NOT NULL AND Activo = 1").HasDatabaseName("IX_IdenExtTokens_RefreshHash");
        builder.HasIndex(t => t.AccessTokenHash).HasFilter("AccessTokenHash IS NOT NULL AND Activo = 1").HasDatabaseName("IX_IdenExtTokens_AccessHash");
    }
}
