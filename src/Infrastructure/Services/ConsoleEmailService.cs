using Microsoft.Extensions.Logging;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services;

/// <summary>
/// Implementación de desarrollo: imprime los emails a la consola en lugar de enviarlos por SMTP.
/// Usar solo en entorno Development.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task EnviarInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║           [DEV] EMAIL DE INVITACIÓN                  ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Link:     {Link}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal, linkInvitacion);

        return Task.CompletedTask;
    }

    public Task EnviarConfirmacionTurnoAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string cancelUrl)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║        [DEV] EMAIL CONFIRMACIÓN DE TURNO             ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:        {Email}
            ║  Cliente:     {Nombre}
            ║  Sucursal:    {Sucursal}
            ║  Servicio:    {Servicio}
            ║  Profesional: {Profesional}
            ║  Inicio:      {Inicio}
            ║  Fin:         {Fin}
            ║  Cancelar:    {CancelUrl}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, profesionalNombre,
            fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"),
            fechaHoraFin.ToString("HH:mm"),
            cancelUrl);

        return Task.CompletedTask;
    }

    public Task EnviarInvitacionUsuarioExistenteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║     [DEV] EMAIL INVITACIÓN (usuario existente)       ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Link:     {Link}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal, linkInvitacion);

        return Task.CompletedTask;
    }

    public Task EnviarReenvioInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║        [DEV] REENVÍO DE INVITACIÓN                   ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Link:     {Link}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal, linkInvitacion);

        return Task.CompletedTask;
    }

    public Task EnviarSolicitudTurnoPendienteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string cancelUrl)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║     [DEV] EMAIL SOLICITUD TURNO PENDIENTE            ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:        {Email}
            ║  Cliente:     {Nombre}
            ║  Sucursal:    {Sucursal}
            ║  Servicio:    {Servicio}
            ║  Profesional: {Profesional}
            ║  Inicio:      {Inicio}
            ║  Fin:         {Fin}
            ║  Cancelar:    {CancelUrl}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, profesionalNombre,
            fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"),
            fechaHoraFin.ToString("HH:mm"),
            cancelUrl);

        return Task.CompletedTask;
    }

    public Task EnviarCancelacionStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║       [DEV] EMAIL CANCELACIÓN POR STAFF              ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Cliente:  {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Servicio: {Servicio}
            ║  Inicio:   {Inicio}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"));

        return Task.CompletedTask;
    }

    public Task EnviarCancelacionClienteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║      [DEV] EMAIL CANCELACIÓN POR CLIENTE             ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Cliente:  {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Servicio: {Servicio}
            ║  Inicio:   {Inicio}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"));

        return Task.CompletedTask;
    }

    public Task EnviarSolicitudTurnoStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string panelUrl,
        string? observaciones = null)
    {
        var obsLinea = string.IsNullOrWhiteSpace(observaciones) ? "" : $"\n            ║  Mensaje:  {observaciones}";
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║   [DEV] SOLICITUD DE TURNO PENDIENTE (staff)         ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Cliente:  {Cliente}
            ║  Servicio: {Servicio}
            ║  Inicio:   {Inicio}
            ║  Fin:      {Fin}
            ║  Panel:    {Panel}{Obs}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            clienteNombre, servicioNombre,
            fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"),
            fechaHoraFin.ToString("HH:mm"),
            panelUrl, obsLinea);

        return Task.CompletedTask;
    }

    public Task EnviarNuevoTurnoStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string? observaciones = null)
    {
        var obsLinea = string.IsNullOrWhiteSpace(observaciones) ? "" : $"\n            ║  Mensaje:  {observaciones}";
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║      [DEV] NUEVO TURNO (notificación staff)          ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Cliente:  {Cliente}
            ║  Servicio: {Servicio}
            ║  Inicio:   {Inicio}
            ║  Fin:      {Fin}{Obs}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            clienteNombre, servicioNombre,
            fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"),
            fechaHoraFin.ToString("HH:mm"),
            obsLinea);

        return Task.CompletedTask;
    }

    public Task EnviarAlertaLiberacionAgendaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║      [DEV] ALERTA HORARIO LIBERADO (profesional)     ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:     {Email}
            ║  Nombre:   {Nombre}
            ║  Sucursal: {Sucursal}
            ║  Servicio: {Servicio}
            ║  Cliente:  {Cliente}
            ║  Inicio:   {Inicio}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, clienteNombre, fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"));

        return Task.CompletedTask;
    }

    public Task EnviarRecuperacionContrasenaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string linkRecuperacion)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║      [DEV] EMAIL RECUPERACIÓN DE CONTRASEÑA          ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:   {Email}
            ║  Nombre: {Nombre}
            ║  Link:   {Link}
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, linkRecuperacion);

        return Task.CompletedTask;
    }

    public Task EnviarBienvenidaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreNegocio,
        string urlPanel)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║           [DEV] EMAIL DE BIENVENIDA                  ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:    {Email}
            ║  Nombre:  {Nombre}
            ║  Negocio: {Negocio}
            ║  Panel:   {Panel}
            ╠══════════════════════════════════════════════════════╣
            ║  Pasos de configuración inicial:                      ║
            ║  Asistente: {Panel}/admin/onboarding                  ║
            ║  - O por secciones:                                   ║
            ║    1. Ajustes   → {Panel}/admin/ajustes               ║
            ║    2. Servicios → {Panel}/admin/servicios             ║
            ║    3. Equipo    → {Panel}/admin/usuarios              ║
            ║  [PDF adjunto: guia-inicio-metaturnos.pdf]            ║
            ╚══════════════════════════════════════════════════════╝
            """,
            destinatarioEmail, destinatarioNombre, nombreNegocio, urlPanel, urlPanel, urlPanel, urlPanel, urlPanel);

        return Task.CompletedTask;
    }

    public Task EnviarRecordatorioAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin,
        string cancelUrl,
        bool es24h)
    {
        _logger.LogWarning(
            """
            ╔══════════════════════════════════════════════════════╗
            ║      [DEV] EMAIL RECORDATORIO {Tipo}                 ║
            ╠══════════════════════════════════════════════════════╣
            ║  Para:        {Email}
            ║  Cliente:     {Nombre}
            ║  Sucursal:    {Sucursal}
            ║  Servicio:    {Servicio}
            ║  Profesional: {Profesional}
            ║  Inicio:      {Inicio}
            ║  Fin:         {Fin}
            ║  Cancelar:    {CancelUrl}
            ╚══════════════════════════════════════════════════════╝
            """,
            es24h ? "24 H" : "3 H",
            destinatarioEmail, destinatarioNombre, nombreSucursal,
            servicioNombre, profesionalNombre,
            fechaHoraInicio.ToString("dd/MM/yyyy HH:mm"),
            fechaHoraFin.ToString("HH:mm"),
            cancelUrl);

        return Task.CompletedTask;
    }
}
