using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class PagoTurnoConfiguration : IEntityTypeConfiguration<PagoTurno>
{
    public void Configure(EntityTypeBuilder<PagoTurno> builder)
    {
        builder.ToTable("pagos_turno");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Concepto)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.Monto)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(p => p.Fecha)
            .IsRequired();

        builder.HasIndex(p => p.TurnoId);
        builder.HasIndex(p => p.MovimientoCajaId).IsUnique();

        builder.HasOne(p => p.Turno)
            .WithMany(t => t.Pagos)
            .HasForeignKey(p => p.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.MovimientoCaja)
            .WithOne(m => m.PagoTurno)
            .HasForeignKey<PagoTurno>(p => p.MovimientoCajaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
