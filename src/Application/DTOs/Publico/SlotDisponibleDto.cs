namespace TurneroApp.Application.DTOs.Publico;

public class SlotDisponibleDto
{
    /// <summary>Hora del slot en formato "HH:mm".</summary>
    public string Hora { get; set; } = string.Empty;

    /// <summary>Id del profesional libre para este slot (asignado automáticamente).</summary>
    public string ProfesionalId { get; set; } = string.Empty;

    /// <summary>Nombre completo del profesional.</summary>
    public string ProfesionalNombre { get; set; } = string.Empty;
}
