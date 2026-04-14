using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class SucursalMercadoPagoConfiguration : IEntityTypeConfiguration<SucursalMercadoPago>
{
    public void Configure(EntityTypeBuilder<SucursalMercadoPago> builder)
    {
        builder.ToTable("sucursal_mercadopago");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.MpUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.AccessToken)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.RefreshToken)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.AccessTokenExpiraEnUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.RefreshTokenExpiraEnUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.FechaConexionUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.UltimaActualizacionUtc)
            .HasColumnType("timestamptz");

        builder.Property(e => e.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(e => e.Sucursal)
            .WithMany()
            .HasForeignKey(e => e.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.SucursalId).IsUnique();
    }
}
