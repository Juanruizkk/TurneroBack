using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Infrastructure.Data.Configurations;

public class ConfiguracionSucursalConfiguration : IEntityTypeConfiguration<ConfiguracionSucursal>
{
    public void Configure(EntityTypeBuilder<ConfiguracionSucursal> builder)
    {
        builder.ToTable("configuraciones_sucursal");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasIndex(c => c.SucursalId).IsUnique();

        builder.Property(c => c.MinutosAnticipacionMinimaReserva)
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(c => c.DiasMaximosReservaFutura)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(c => c.MinutosBufferEntreTurnos)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.PermiteCancelacionCliente)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.HorasMinimasCancelacion)
            .IsRequired()
            .HasDefaultValue(24);

        builder.Property(c => c.EnviaRecordatorioEmail)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.EnviaRecordatorioWhatsapp)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.ModoConfirmacionTurnos)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ModoConfirmacionTurnos.Automatico);

        builder.Property(c => c.HorariosAtencionJson)
            .HasColumnName("HorariosAtencion")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(c => c.MensajeWhatsApp24h)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(c => c.MensajeWhatsApp3h)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.HasOne(c => c.Sucursal)
            .WithOne(s => s.ConfiguracionSucursal)
            .HasForeignKey<ConfiguracionSucursal>(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
