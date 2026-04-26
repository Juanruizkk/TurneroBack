using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Apellido)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Telefono)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.FechaArchivado)
            .IsRequired(false);

        // Unicidad de teléfono por sucursal se valida en capa de servicio.
        // Para agregar el constraint de DB, limpiar datos duplicados y crear una nueva migración con .IsUnique().
        builder.HasIndex(c => new { c.SucursalId, c.Telefono });

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.HasIndex(c => new { c.SucursalId, c.Email });

        builder.Property(c => c.Observaciones)
            .HasMaxLength(1000);

        builder.HasMany(c => c.Turnos)
            .WithOne(t => t.Cliente)
            .HasForeignKey(t => t.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
