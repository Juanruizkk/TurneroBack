using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class UsuarioSucursalRolConfiguration : IEntityTypeConfiguration<UsuarioSucursalRol>
{
    public void Configure(EntityTypeBuilder<UsuarioSucursalRol> builder)
    {
        builder.ToTable("usuario_sucursal_roles");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Rol)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.Estado)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.EsOwner)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.InvitacionToken)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.FechaExpiracionInvitacion)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(u => u.FechaCreacion)
            .IsRequired()
            .HasColumnType("timestamptz");

        // Unique: un usuario no puede estar dos veces en la misma sucursal
        builder.HasIndex(u => new { u.UsuarioId, u.SucursalId }).IsUnique();

        // Índice filtrado único sobre InvitacionToken (solo filas no nulas)
        builder.HasIndex(u => u.InvitacionToken)
            .IsUnique()
            .HasFilter("\"InvitacionToken\" IS NOT NULL");

        // ─── Relaciones ───────────────────────────────────────────────────────

        builder.HasOne(u => u.Usuario)
            .WithMany(u => u.Sucursales)
            .HasForeignKey(u => u.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Sucursal)
            .WithMany(s => s.UsuarioSucursalRoles)
            .HasForeignKey(u => u.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Permisos)
            .WithOne(p => p.UsuarioSucursalRol)
            .HasForeignKey(p => p.UsuarioSucursalRolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.HorariosLaborales)
            .WithOne(h => h.UsuarioSucursalRol)
            .HasForeignKey(h => h.UsuarioSucursalRolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Servicios)
            .WithOne(s => s.UsuarioSucursalRol)
            .HasForeignKey(s => s.UsuarioSucursalRolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
