using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ConfProvIdenConfiguration : IEntityTypeConfiguration<ConfProvIden>
{
    public void Configure(EntityTypeBuilder<ConfProvIden> builder)
    {
        builder.ToTable("ConfProvIden");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.IdTenant).IsRequired();
        builder.Property(c => c.IdProvIden).IsRequired();
        builder.Property(c => c.ClientId).HasMaxLength(500).IsRequired();
        builder.Property(c => c.ClientSecret).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Scopes).HasMaxLength(500);
        builder.Property(c => c.Callback).HasMaxLength(500).IsRequired();
        builder.Property(c => c.RedirectUri).HasMaxLength(500);
        builder.Property(c => c.RolDefecto);
        builder.Property(c => c.GuardarTokens).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.PermitirAutoLink).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.AutoProvisionar).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.RequiereMFALocal).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.RequireEmailVerified).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.AllowLoginWithoutRefreshToken).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.AllowRefreshTokenRotation).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.Estado).HasDefaultValue((byte)1).IsRequired();
        builder.Property(c => c.Metadata).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.FecCrea).HasDefaultValueSql("sysutcdatetime()");
        builder.Property(c => c.FecMod);

        builder.Property(c => c.PermitirLogin).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirCrearUsuario).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirVincular).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirDesvincular).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirPasswordLocal).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.ObligaMFA).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.PermitirCambioEmail).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirCambioNombre).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirSincronizarAvatar).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.PermitirSincronizarPerfil).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.FrecuenciaSincronizacion).HasMaxLength(20).HasDefaultValue("Siempre").IsRequired();
        builder.Property(c => c.Prioridad).HasDefaultValue(0).IsRequired();
        builder.Property(c => c.OrdenVisual).HasDefaultValue(0).IsRequired();
        builder.Property(c => c.Logo).HasMaxLength(500);
        builder.Property(c => c.Color).HasMaxLength(20);
        builder.Property(c => c.Tooltip).HasMaxLength(200);
        builder.Property(c => c.Descripcion).HasMaxLength(500);
        builder.Property(c => c.AuthorizationEndpoint).HasMaxLength(500);
        builder.Property(c => c.TokenEndpoint).HasMaxLength(500);
        builder.Property(c => c.JwksUri).HasMaxLength(500);
        builder.Property(c => c.Issuer).HasMaxLength(500);
        builder.Property(c => c.ResponseType).HasMaxLength(50).HasDefaultValue("code").IsRequired();
        builder.Property(c => c.GrantType).HasMaxLength(50).HasDefaultValue("authorization_code").IsRequired();
        builder.Property(c => c.ExtraParams).HasMaxLength(1000);

        builder.Property(c => c.RowVersion).IsRowVersion().IsRequired();

        builder.HasOne(c => c.Tenant)
            .WithMany(t => t.ConfProvIden)
            .HasForeignKey(c => c.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ProvIden)
            .WithMany(p => p.Configuraciones)
            .HasForeignKey(c => c.IdProvIden)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.RolDefectoNav)
            .WithMany()
            .HasForeignKey(c => c.RolDefecto)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.IdTenant, c.IdProvIden }).IsUnique().HasDatabaseName("UK_ConfProvIden_TenantProveedor");
        builder.HasIndex(c => new { c.IdTenant, c.IdProvIden }).HasFilter("Activo = 1").HasDatabaseName("IX_ConfProvIden_Activo");
    }
}
