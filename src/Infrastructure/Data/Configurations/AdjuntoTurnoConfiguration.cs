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

        // Columna renombrada en C# (Url) pero mantiene el nombre físico UrlArchivo en DB
        builder.Property(a => a.Url)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("UrlArchivo");

        builder.Property(a => a.PublicId)
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.NombreArchivo)
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.TipoRecurso)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("image");

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
