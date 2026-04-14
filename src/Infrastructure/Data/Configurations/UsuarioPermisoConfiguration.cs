using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class UsuarioPermisoConfiguration : IEntityTypeConfiguration<UsuarioPermiso>
{
    public void Configure(EntityTypeBuilder<UsuarioPermiso> builder)
    {
        builder.ToTable("usuario_permisos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UsuarioSucursalRolId)
            .IsRequired();

        builder.Property(p => p.Permiso)
            .IsRequired()
            .HasConversion<int>();

        // Un mismo permiso no puede aparecer dos veces para el mismo rol
        builder.HasIndex(p => new { p.UsuarioSucursalRolId, p.Permiso }).IsUnique();

        // Índice para consultas por rol
        builder.HasIndex(p => p.UsuarioSucursalRolId);

        // La relación inversa está configurada en UsuarioSucursalRolConfiguration.
    }
}
