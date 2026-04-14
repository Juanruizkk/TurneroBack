using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Apellido)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);

        // Email único globalmente
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Telefono)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.FechaCreacion)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(u => u.TokenRecuperacion)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.TokenRecuperacionExpiracion)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(u => u.TokenRecuperacion)
            .IsUnique()
            .HasFilter("\"TokenRecuperacion\" IS NOT NULL");

        builder.Property(u => u.SeleccionToken)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.SeleccionTokenExpiracion)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(u => u.SeleccionToken)
            .HasFilter("\"SeleccionToken\" IS NOT NULL");

        // ─── Relaciones ──────────────────────────────────────────────────────

        builder.HasMany(u => u.TurnosComoProfesional)
            .WithOne(t => t.Profesional)
            .HasForeignKey(t => t.ProfesionalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.MovimientosRegistrados)
            .WithOne(m => m.Usuario)
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ComentariosRealizados)
            .WithOne(c => c.Autor)
            .HasForeignKey(c => c.AutorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.AdjuntosSubidos)
            .WithOne(a => a.Autor)
            .HasForeignKey(a => a.AutorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
