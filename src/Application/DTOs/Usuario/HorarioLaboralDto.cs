namespace TurneroApp.Application.DTOs.Usuario;

/// <summary>DiaSemana es int (0=Domingo … 6=Sábado) para evitar serialización como string.</summary>
public record HorarioLaboralDto(int DiaSemana, string HoraDesde, string HoraHasta, bool Activo);
