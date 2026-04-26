using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class ServicioConfiguration : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> builder)
    {
        builder.ToTable("servicios");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Descripcion)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.Aclaraciones)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.Precio)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.DuracionClienteMin)
            .IsRequired();

        builder.Property(s => s.DuracionAgendaMin)
            .IsRequired();

        builder.Property(s => s.RequiereSena)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.TipoSena)
            .HasConversion<int?>();

        builder.Property(s => s.ValorSena)
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.ModalidadPrestacion)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.TipoServicio)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasMany(s => s.Turnos)
            .WithOne(t => t.Servicio)
            .HasForeignKey(t => t.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        // B-02: concurrencia optimista — usa la columna de sistema xmin de PostgreSQL.
        builder.Property(s => s.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
