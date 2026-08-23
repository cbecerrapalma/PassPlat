using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class IdenExtConfiguration : IEntityTypeConfiguration<IdenExt>
{
    public void Configure(EntityTypeBuilder<IdenExt> builder)
    {
        builder.ToTable("IdenExt");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.IdUsuario).IsRequired();
        builder.Property(i => i.IdProvIden).IsRequired();
        builder.Property(i => i.IdTenant).IsRequired();
        builder.Property(i => i.SubExterno).HasMaxLength(255).IsRequired();
        builder.Property(i => i.ProviderUserName).HasMaxLength(255);
        builder.Property(i => i.EmailExterno).HasMaxLength(255);
        builder.Property(i => i.NombreExterno).HasMaxLength(255);
        builder.Property(i => i.Avatar).HasMaxLength(500);
        builder.Property(i => i.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(i => i.ClaimsJson).HasColumnType("nvarchar(max)");
        builder.Property(i => i.AccessToken).HasColumnType("varbinary(2000)");
        builder.Property(i => i.RefreshToken).HasColumnType("varbinary(2000)");
        builder.Property(i => i.IdToken).HasColumnType("varbinary(3000)");
        builder.Property(i => i.TokenExpiration);
        builder.Property(i => i.CorrelationId);
        builder.Property(i => i.EsPrincipal).HasDefaultValue(false).IsRequired();
        builder.Property(i => i.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(i => i.Eliminado).HasDefaultValue(false).IsRequired();
        builder.Property(i => i.IdEstado);
        builder.Property(i => i.Scopes).HasMaxLength(1000);
        builder.Property(i => i.UltimaIP).HasMaxLength(45);
        builder.Property(i => i.UltimoDisp);
        builder.Property(i => i.UltimoUserAgent).HasMaxLength(500);
        builder.Property(i => i.UltimoTenant);
        builder.Property(i => i.FecRevocacion);
        builder.Property(i => i.IdUsuarioRevoca);
        builder.Property(i => i.MotivoRevocacion).HasMaxLength(500);
        builder.Property(i => i.FecEliminacion);
        builder.Property(i => i.IdUsuarioElimina);
        builder.Property(i => i.UltimoLogin);
        builder.Property(i => i.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(i => i.FecMod);

        builder.HasOne(i => i.Usuario)
            .WithMany(u => u.IdenExt)
            .HasForeignKey(i => i.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProvIden)
            .WithMany(p => p.IdenExt)
            .HasForeignKey(i => i.IdProvIden)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Tenant)
            .WithMany(t => t.IdenExt)
            .HasForeignKey(i => i.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.UsuarioElimina)
            .WithMany()
            .HasForeignKey(i => i.IdUsuarioElimina)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.UsuarioRevoca)
            .WithMany()
            .HasForeignKey(i => i.IdUsuarioRevoca)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Estado)
            .WithMany(e => e.IdenExt)
            .HasForeignKey(i => i.IdEstado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Dispositivo)
            .WithMany()
            .HasForeignKey(i => i.UltimoDisp)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.UltimoTenantNav)
            .WithMany()
            .HasForeignKey(i => i.UltimoTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.IdProvIden, i.SubExterno }).IsUnique().HasDatabaseName("UK_IdenExt_ProveedorSub");
        builder.HasIndex(i => new { i.IdUsuario, i.IdProvIden }).IsUnique().HasDatabaseName("UK_IdenExt_UsuarioProveedor");
        builder.HasIndex(i => i.IdUsuario).HasFilter("Eliminado = 0").HasDatabaseName("IX_IdenExt_Usuario");
        builder.HasIndex(i => i.EmailExterno).HasFilter("EmailExterno IS NOT NULL AND Eliminado = 0").HasDatabaseName("IX_IdenExt_EmailExterno");
        builder.HasIndex(i => i.UltimoLogin).IsDescending(true).HasFilter("Activo = 1 AND Eliminado = 0").HasDatabaseName("IX_IdenExt_UltimoLogin");
    }
}
