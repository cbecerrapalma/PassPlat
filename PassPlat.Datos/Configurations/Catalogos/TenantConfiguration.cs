using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", tb =>
        {
            tb.HasTrigger("TR_Tenants_NoDeleteSistema");
            tb.HasTrigger("TR_Tenants_ProtegerSistema");
        });
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.Codigo).HasMaxLength(20).IsRequired().IsUnicode(false);
        builder.Property(t => t.Nombre).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(t => t.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(t => t.EsSistema).HasDefaultValue(false).IsRequired();
        builder.Property(t => t.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();

        builder.HasIndex(t => t.Codigo).IsUnique();
        builder.HasIndex(t => t.EsSistema).IsUnique().HasFilter("EsSistema = 1").HasDatabaseName("UX_Tenants_EsSistema");
    }
}
