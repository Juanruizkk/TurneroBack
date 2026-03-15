using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class HorarioLaboralConfiguration : IEntityTypeConfiguration<HorarioLaboral>
{
    public void Configure(EntityTypeBuilder<HorarioLaboral> builder)
    {
        builder.ToTable("horarios_laborales");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.DiaSemana)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(h => h.HoraDesde)
            .IsRequired();

        builder.Property(h => h.HoraHasta)
            .IsRequired();

        builder.Property(h => h.Activo)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
