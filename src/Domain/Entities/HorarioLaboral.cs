namespace TurneroApp.Domain.Entities;

public class HorarioLaboral
{
    public Guid Id { get; set; }
    public Guid UsuarioSucursalRolId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeSpan HoraDesde { get; set; }
    public TimeSpan HoraHasta { get; set; }
    public bool Activo { get; set; } = true;

    // Navigation properties
    public UsuarioSucursalRol UsuarioSucursalRol { get; set; } = null!;
}
