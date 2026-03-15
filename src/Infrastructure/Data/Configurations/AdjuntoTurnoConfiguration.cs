using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class AdjuntoTurnoConfiguration : IEntityTypeConfiguration<AdjuntoTurno>
{
    public void Configure(EntityTypeBuilder<AdjuntoTurno> builder)
    {
        builder.ToTable("adjuntos_turno");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UrlArchivo)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.Descripcion)
            .HasMaxLength(500);

        builder.Property(a => a.Fecha)
            .IsRequired();

        builder.HasOne(a => a.Turno)
            .WithMany(t => t.Adjuntos)
            .HasForeignKey(a => a.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Autor)
            .WithMany(u => u.AdjuntosSubidos)
            .HasForeignKey(a => a.AutorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
