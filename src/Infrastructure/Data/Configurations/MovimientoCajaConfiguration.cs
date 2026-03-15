using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class MovimientoCajaConfiguration : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("movimientos_caja");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.TipoMovimiento)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.MedioPago)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.Monto)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(m => m.Observaciones)
            .HasMaxLength(1000);

        builder.Property(m => m.Fecha)
            .IsRequired();

        builder.HasIndex(m => new { m.CajaId, m.Fecha });
        builder.HasIndex(m => m.TurnoId);

        builder.HasOne(m => m.Caja)
            .WithMany(c => c.Movimientos)
            .HasForeignKey(m => m.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
            .WithMany(u => u.MovimientosRegistrados)
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Turno)
            .WithMany()
            .HasForeignKey(m => m.TurnoId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(m => m.PagoTurno)
            .WithOne(p => p.MovimientoCaja)
            .HasForeignKey<PagoTurno>(p => p.MovimientoCajaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
