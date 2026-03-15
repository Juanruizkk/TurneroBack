using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class BloqueoAgendaConfiguration : IEntityTypeConfiguration<BloqueoAgenda>
{
    public void Configure(EntityTypeBuilder<BloqueoAgenda> builder)
    {
        builder.ToTable("bloqueos_agenda");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Desde)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(b => b.Hasta)
            .IsRequired()
            .HasColumnType("timestamp without time zone");

        builder.Property(b => b.Motivo)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(b => new { b.SucursalId, b.Desde, b.Hasta });

        builder.HasOne(b => b.Sucursal)
            .WithMany(s => s.BloqueoAgendas)
            .HasForeignKey(b => b.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Usuario)
            .WithMany()
            .HasForeignKey(b => b.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
