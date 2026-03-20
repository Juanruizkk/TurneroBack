using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class UsuarioServicioConfiguration : IEntityTypeConfiguration<UsuarioServicio>
{
    public void Configure(EntityTypeBuilder<UsuarioServicio> builder)
    {
        builder.ToTable("usuario_servicios");

        builder.HasKey(us => new { us.UsuarioId, us.ServicioId });

        builder.HasOne(us => us.Usuario)
            .WithMany(u => u.Servicios)
            .HasForeignKey(us => us.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Servicio)
            .WithMany()
            .HasForeignKey(us => us.ServicioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
