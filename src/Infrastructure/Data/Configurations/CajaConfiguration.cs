using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class CajaConfiguration : IEntityTypeConfiguration<Caja>
{
    public void Configure(EntityTypeBuilder<Caja> builder)
    {
        builder.ToTable("cajas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(c => c.SucursalId).IsUnique();

        builder.HasMany(c => c.Movimientos)
            .WithOne(m => m.Caja)
            .HasForeignKey(m => m.CajaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
