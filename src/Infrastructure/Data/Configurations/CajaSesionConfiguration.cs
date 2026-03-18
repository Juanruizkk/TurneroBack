using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class CajaSesionConfiguration : IEntityTypeConfiguration<CajaSesion>
{
    public void Configure(EntityTypeBuilder<CajaSesion> builder)
    {
        builder.ToTable("caja_sesiones");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FechaApertura)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(s => s.FechaCierre)
            .HasColumnType("timestamp without time zone");

        builder.Property(s => s.MontoInicial)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.MontoFinalEsperado)
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.MontoFinalReal)
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.Diferencia)
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.Estado)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.ObservacionesApertura)
            .HasMaxLength(500);

        builder.Property(s => s.ObservacionesCierre)
            .HasMaxLength(500);

        // Índice para query "sesión activa" (frecuente)
        builder.HasIndex(s => new { s.CajaId, s.Estado });

        // FK → Caja
        builder.HasOne(s => s.Caja)
            .WithMany(c => c.Sesiones)
            .HasForeignKey(s => s.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → Usuario apertura (nullable, sin cascade para evitar ciclos)
        builder.HasOne(s => s.UsuarioApertura)
            .WithMany()
            .HasForeignKey(s => s.UsuarioAperturaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // FK → Usuario cierre (nullable)
        builder.HasOne(s => s.UsuarioCierre)
            .WithMany()
            .HasForeignKey(s => s.UsuarioCierreId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
