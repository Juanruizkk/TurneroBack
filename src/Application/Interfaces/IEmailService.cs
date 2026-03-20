namespace TurneroApp.Application.Interfaces;

public interface IEmailService
{
    Task EnviarInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion);

    Task EnviarReenvioInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion);

    Task EnviarConfirmacionTurnoAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string cancelUrl);

    /// <summary>Avisa al cliente que el staff canceló su turno e invita a reprogramar.</summary>
    Task EnviarCancelacionStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio);

    /// <summary>Confirma al cliente que su cancelación fue registrada.</summary>
    Task EnviarCancelacionClienteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio);

    /// <summary>Alerta al profesional (o sucursal) que un cliente canceló y el slot quedó libre.</summary>
    Task EnviarAlertaLiberacionAgendaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio);

    /// <summary>Envía el link de recuperación de contraseña al usuario.</summary>
    Task EnviarRecuperacionContrasenaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string linkRecuperacion);

    /// <summary>
    /// Recordatorio automático enviado antes del turno.
    /// <paramref name="es24h"/>: <c>true</c> para el recordatorio de 24 h, <c>false</c> para el de 3 h.
    /// </summary>
    Task EnviarRecordatorioAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string cancelUrl,
        bool es24h);
}
