using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.Datos.Configurations.Catalogos;

public class ConfigAppConfiguration : IEntityTypeConfiguration<ConfigApp>
{
    public void Configure(EntityTypeBuilder<ConfigApp> builder)
    {
        builder.ToTable("ConfigApp", t => t.HasCheckConstraint("CK_ConfApp_Tipo", "Tipo IN ('string', 'int', 'bool', 'json', 'encrypted')"));
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Property(c => c.IdTenant);
        builder.Property(c => c.Grupo).HasMaxLength(50).IsRequired().IsUnicode(false).HasDefaultValue("General");
        builder.Property(c => c.Clave).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(c => c.Valor).HasMaxLength(-1).IsRequired();
        builder.Property(c => c.Tipo).HasMaxLength(10).IsRequired().IsUnicode(false).HasDefaultValue("string");
        builder.Property(c => c.Descripcion).HasMaxLength(255);
        builder.Property(c => c.EsEncriptado).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.Activo).HasDefaultValue(true).IsRequired();
        builder.Property(c => c.IdUsrMod);
        builder.Property(c => c.FecCrea).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(c => c.FecMod);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.IdTenant, c.Clave }).IsUnique();
        builder.HasIndex(c => c.IdTenant).HasFilter("IdTenant IS NOT NULL");
    }
}
