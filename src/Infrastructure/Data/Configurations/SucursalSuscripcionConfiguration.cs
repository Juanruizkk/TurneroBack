using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class SucursalSuscripcionConfiguration : IEntityTypeConfiguration<SucursalSuscripcion>
{
    public void Configure(EntityTypeBuilder<SucursalSuscripcion> builder)
    {
        builder.ToTable("sucursal_suscripciones");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.FechaInicio)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.FechaVencimiento)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.EnPeriodoPrueba)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(s => s.Sucursal)
            .WithMany()
            .HasForeignKey(s => s.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Suscripciones)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.SucursalId);
    }
}
