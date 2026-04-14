using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("sucursales");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(s => s.Slug)
            .IsUnique()
            .HasDatabaseName("IX_sucursales_slug");

        builder.Property(s => s.Slogan)
            .HasMaxLength(300)
            .IsRequired(false);

        builder.Property(s => s.FotoUrl)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(s => s.FotoPublicId)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(s => s.BackgroundUrl)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(s => s.BackgroundPublicId)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(s => s.Moneda)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("ARS");

        builder.Property(s => s.Rubros)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .IsRequired()
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(s => s.Direccion)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.Telefono)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnType("timestamptz");

        // Relación con UsuarioSucursalRol configurada en UsuarioSucursalRolConfiguration.

        builder.HasMany(s => s.Servicios)
            .WithOne(sv => sv.Sucursal)
            .HasForeignKey(sv => sv.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Clientes)
            .WithOne(c => c.Sucursal)
            .HasForeignKey(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Turnos)
            .WithOne(t => t.Sucursal)
            .HasForeignKey(t => t.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Caja)
            .WithOne(c => c.Sucursal)
            .HasForeignKey<Caja>(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.BloqueoAgendas)
            .WithOne(b => b.Sucursal)
            .HasForeignKey(b => b.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ConfiguracionSucursal)
            .WithOne(c => c.Sucursal)
            .HasForeignKey<ConfiguracionSucursal>(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
