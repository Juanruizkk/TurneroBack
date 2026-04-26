using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class PagoSuscripcionConfiguration : IEntityTypeConfiguration<PagoSuscripcion>
{
    public void Configure(EntityTypeBuilder<PagoSuscripcion> builder)
    {
        builder.ToTable("pagos_suscripcion");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.SucursalId).IsRequired();
        builder.Property(p => p.PlanId).IsRequired();

        builder.Property(p => p.PlanNombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Monto)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.MPPaymentId).IsRequired();

        builder.Property(p => p.FechaPago)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne(p => p.Sucursal)
            .WithMany()
            .HasForeignKey(p => p.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.SucursalId);
        builder.HasIndex(p => p.MPPaymentId).IsUnique();
    }
}
