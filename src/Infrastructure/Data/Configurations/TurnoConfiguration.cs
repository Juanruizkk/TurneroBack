using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class TurnoConfiguration : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("turnos");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.FechaHoraInicio)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(t => t.FechaHoraFin)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.HasIndex(t => new { t.ProfesionalId, t.FechaHoraInicio });
        builder.HasIndex(t => t.TokenGestion).IsUnique();
        builder.HasIndex(t => new { t.SucursalId, t.FechaHoraInicio });

        builder.Property(t => t.Estado)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Origen)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Observaciones)
            .HasMaxLength(1000);

        builder.Property(t => t.TokenGestion)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.PrecioServicio)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(t => t.MontoSena)
            .HasColumnType("numeric(18,2)");

        builder.Property(t => t.TotalCobrado)
            .IsRequired()
            .HasColumnType("numeric(18,2)")
            .HasDefaultValue(0m);

        builder.Property(t => t.MpPreferenceId)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(t => t.MpCheckoutUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(t => t.Recordatorio24hEnviado)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.Recordatorio3hEnviado)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.FechaCreacion)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.HasMany(t => t.Comentarios)
            .WithOne(c => c.Turno)
            .HasForeignKey(c => c.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Adjuntos)
            .WithOne(a => a.Turno)
            .HasForeignKey(a => a.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Pagos)
            .WithOne(p => p.Turno)
            .HasForeignKey(p => p.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Avoid cascade cycles - Sucursal, Cliente, Servicio, Profesional use Restrict
        builder.HasOne(t => t.Sucursal)
            .WithMany(s => s.Turnos)
            .HasForeignKey(t => t.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Cliente)
            .WithMany(c => c.Turnos)
            .HasForeignKey(t => t.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Servicio)
            .WithMany(s => s.Turnos)
            .HasForeignKey(t => t.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Profesional)
            .WithMany(u => u.TurnosComoProfesional)
            .HasForeignKey(t => t.ProfesionalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
