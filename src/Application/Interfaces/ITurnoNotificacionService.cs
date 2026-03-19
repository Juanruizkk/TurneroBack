using TurneroApp.Application.DTOs.Turno;

namespace TurneroApp.Application.Interfaces;

public interface ITurnoNotificacionService
{
    /// <summary>Notifica a profesional y admins de la sucursal que se creó un nuevo turno.</summary>
    Task NotificarNuevoTurnoAsync(TurnoResponseDto turno);

    /// <summary>Notifica a profesional y admins de la sucursal que un turno fue cancelado.</summary>
    Task NotificarCancelacionAsync(TurnoResponseDto turno);
}
