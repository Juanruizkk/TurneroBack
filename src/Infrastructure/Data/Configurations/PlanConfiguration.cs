using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("planes");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Descripcion)
            .HasMaxLength(300)
            .IsRequired(false);

        builder.Property(p => p.PrecioMensual)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.MaxUsuarios)
            .IsRequired();

        builder.Property(p => p.MaxTurnosMes)
            .IsRequired(false);

        builder.Property(p => p.EsActivo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.OrdenDisplay)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.DuracionTrialDias)
            .IsRequired()
            .HasDefaultValue(14);

        builder.Property(p => p.TieneReportes)
            .IsRequired()
            .HasDefaultValue(false);
    }
}
