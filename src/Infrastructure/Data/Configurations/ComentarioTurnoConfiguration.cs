using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class ComentarioTurnoConfiguration : IEntityTypeConfiguration<ComentarioTurno>
{
    public void Configure(EntityTypeBuilder<ComentarioTurno> builder)
    {
        builder.ToTable("comentarios_turno");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Texto)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.Fecha)
            .IsRequired();

        builder.HasOne(c => c.Turno)
            .WithMany(t => t.Comentarios)
            .HasForeignKey(c => c.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Autor)
            .WithMany(u => u.ComentariosRealizados)
            .HasForeignKey(c => c.AutorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
