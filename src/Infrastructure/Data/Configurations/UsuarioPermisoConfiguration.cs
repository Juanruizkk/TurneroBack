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

        builder.Property(p => p.UsuarioId)
            .IsRequired();

        builder.Property(p => p.Permiso)
            .IsRequired()
            .HasConversion<int>();

        // Un mismo permiso no puede aparecer dos veces para el mismo usuario
        builder.HasIndex(p => new { p.UsuarioId, p.Permiso }).IsUnique();

        // Índice para consultas por usuario
        builder.HasIndex(p => p.UsuarioId);

        // La relación inversa ya está configurada en UsuarioConfiguration.
        // No es necesario redefinirla aquí.
    }
}
