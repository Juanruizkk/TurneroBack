namespace TurneroApp.Domain.Entities;

public class ServicioDiaDisponible
{
    public Guid ServicioId { get; set; }
    public DayOfWeek DiaSemana { get; set; }

    // Navigation property
    public Servicio Servicio { get; set; } = null!;
}
