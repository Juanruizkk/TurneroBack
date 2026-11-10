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

    /// <summary>
    /// Envía una invitación a un usuario que ya tiene cuenta en otra sucursal.
    /// No pide contraseña — solo aceptar para unirse.
    /// </summary>
    Task EnviarInvitacionUsuarioExistenteAsync(
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

    /// <summary>
    /// Avisa al cliente que su solicitud de turno fue recibida y está pendiente de confirmación por el negocio.
    /// Se envía cuando la sucursal opera en modo de confirmación manual.
    /// </summary>
    Task EnviarSolicitudTurnoPendienteAsync(
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

    /// <summary>
    /// Notifica al profesional/admin que llegó una solicitud de turno pendiente de confirmación.
    /// Se usa en modo de confirmación manual.
    /// </summary>
    Task EnviarSolicitudTurnoStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string panelUrl,
        string? observaciones = null);

    /// <summary>Notifica al profesional (o sucursal) que se acaba de reservar un nuevo turno.</summary>
    Task EnviarNuevoTurnoStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string? observaciones = null);

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

    /// <summary>Email de bienvenida enviado tras el registro self-service.</summary>
    Task EnviarBienvenidaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreNegocio,
        string urlPanel);

    /// <summary>
    /// Envía a la casilla de soporte un reporte de problema enviado por un usuario
    /// desde el footer público. <paramref name="emailContacto"/> es opcional.
    /// </summary>
    Task EnviarReporteProblemaAsync(
        string soporteEmail,
        string descripcion,
        string? emailContacto,
        string? urlOrigen,
        string? userAgent);

    /// <summary>
    /// Envía a la casilla de soporte una solicitud de ampliación de plan hecha por el
    /// owner de una sucursal (más usuarios o turnos). El superadmin la procesa a mano.
    /// </summary>
    Task EnviarSolicitudAmpliacionAsync(
        string soporteEmail,
        string sucursalNombre,
        string solicitanteNombre,
        string solicitanteEmail,
        string usoResumen,
        string? mensaje);

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
