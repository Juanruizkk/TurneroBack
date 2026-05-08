using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("notificaciones");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Mensaje)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Tipo)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(n => n.Leida)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.FechaCreacion)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        // Índice para la query más frecuente: no leídas de una sucursal
        builder.HasIndex(n => new { n.SucursalId, n.Leida });
    }
}
