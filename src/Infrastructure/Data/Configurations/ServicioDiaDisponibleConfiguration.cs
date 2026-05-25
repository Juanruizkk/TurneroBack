using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class ServicioDiaDisponibleConfiguration : IEntityTypeConfiguration<ServicioDiaDisponible>
{
    public void Configure(EntityTypeBuilder<ServicioDiaDisponible> builder)
    {
        builder.ToTable("servicio_dias_disponibles");

        builder.HasKey(d => new { d.ServicioId, d.DiaSemana });

        builder.Property(d => d.DiaSemana)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(d => d.Servicio)
            .WithMany(s => s.DiasDisponibles)
            .HasForeignKey(d => d.ServicioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
