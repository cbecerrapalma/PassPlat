using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PassPlat.Dominio.Entities.Core;

namespace PassPlat.Datos.Configurations.Core;

public class GrupoUsuarioConfiguration : IEntityTypeConfiguration<GrupoUsuario>
{
    public void Configure(EntityTypeBuilder<GrupoUsuario> builder)
    {
        builder.ToTable("GruposUsuarios", tb => tb.HasTrigger("TR_GruposUsuarios_ValidarTenant"));
        builder.HasKey(gu => gu.Id);

        builder.Property(gu => gu.Id).ValueGeneratedOnAdd();
        builder.Property(gu => gu.IdGrupo).IsRequired();
        builder.Property(gu => gu.IdUsuario).IsRequired();
        builder.Property(gu => gu.FecAlta).HasDefaultValueSql("sysutcdatetime()").IsRequired();
        builder.Property(gu => gu.IdUsrMod);

        builder.HasOne(gu => gu.Grupo)
            .WithMany(g => g.GruposUsuarios)
            .HasForeignKey(gu => gu.IdGrupo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(gu => gu.Usuario)
            .WithMany(u => u.GruposUsuarios)
            .HasForeignKey(gu => gu.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(gu => gu.UsrMod)
            .WithMany(u => u.GruposUsuariosModificados)
            .HasForeignKey(gu => gu.IdUsrMod)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(gu => new { gu.IdGrupo, gu.IdUsuario }).IsUnique();
    }
}
