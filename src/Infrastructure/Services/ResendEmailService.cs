using Microsoft.Extensions.Configuration;
using Resend;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string  _fromAddress;

    public ResendEmailService(IResend resend, IConfiguration config)
    {
        _resend = resend;
        var fromEmail = config["Resend:FromEmail"] ?? "noreply@turneroapp.com";
        var fromName  = config["Resend:FromName"]  ?? "MetaTurnos";
        _fromAddress  = $"{fromName} <{fromEmail}>";
    }

    public Task EnviarInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        var asunto = $"Tu invitación para unirte a {nombreSucursal}";
        var cuerpo = PlantillaInvitacion(destinatarioNombre, nombreSucursal, linkInvitacion, esReenvio: false);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarReenvioInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        var asunto = $"Nuevo enlace de invitación — {nombreSucursal}";
        var cuerpo = PlantillaInvitacion(destinatarioNombre, nombreSucursal, linkInvitacion, esReenvio: true);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarInvitacionUsuarioExistenteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        var asunto = $"Te invitaron a unirte a {nombreSucursal}";
        var cuerpo = PlantillaInvitacionExistente(destinatarioNombre, nombreSucursal, linkInvitacion);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarBienvenidaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreNegocio,
        string urlPanel)
    {
        var asunto = $"¡Bienvenido a MetaTurnos, {destinatarioNombre}!";
        var cuerpo = PlantillaBienvenida(destinatarioNombre, nombreNegocio, urlPanel);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
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
        var asunto = $"Turno confirmado en {nombreSucursal} — {fechaHoraInicio:dd/MM/yyyy HH:mm}";
        var cuerpo = PlantillaConfirmacionTurno(destinatarioNombre, nombreSucursal, servicioNombre, profesionalNombre, fechaHoraInicio, fechaHoraFin, cancelUrl);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarCancelacionStaffAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio)
    {
        var asunto = $"Tu turno en {nombreSucursal} fue cancelado";
        var cuerpo = PlantillaCancelacionStaff(destinatarioNombre, nombreSucursal, servicioNombre, fechaHoraInicio);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarCancelacionClienteAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        DateTime fechaHoraInicio)
    {
        var asunto = $"Cancelación confirmada — {nombreSucursal}";
        var cuerpo = PlantillaCancelacionCliente(destinatarioNombre, nombreSucursal, servicioNombre, fechaHoraInicio);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
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
        var asunto = $"Nuevo turno reservado — {clienteNombre} el {fechaHoraInicio:dd/MM HH:mm}";
        var cuerpo = PlantillaNuevoTurnoStaff(destinatarioNombre, nombreSucursal, servicioNombre, clienteNombre, fechaHoraInicio, fechaHoraFin, observaciones);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarAlertaLiberacionAgendaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string clienteNombre,
        DateTime fechaHoraInicio)
    {
        var asunto = $"Horario liberado — {servicioNombre} el {fechaHoraInicio:dd/MM HH:mm}";
        var cuerpo = PlantillaAlertaLiberacion(destinatarioNombre, nombreSucursal, servicioNombre, clienteNombre, fechaHoraInicio);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    public Task EnviarRecuperacionContrasenaAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string linkRecuperacion)
    {
        var asunto = "Recuperación de contraseña — MetaTurnos";
        var cuerpo = PlantillaRecuperacionContrasena(destinatarioNombre, linkRecuperacion);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
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
        var etiqueta = es24h ? "mañana" : "en 3 horas";
        var asunto   = $"Recordatorio: tu turno en {nombreSucursal} es {etiqueta} — {fechaHoraInicio:dd/MM HH:mm}";
        var cuerpo   = PlantillaRecordatorio(destinatarioNombre, nombreSucursal, servicioNombre, profesionalNombre, fechaHoraInicio, fechaHoraFin, cancelUrl, es24h);
        return EnviarAsync(destinatarioEmail, asunto, cuerpo);
    }

    // ─── Core ─────────────────────────────────────────────────────────────────

    private async Task EnviarAsync(string destinatarioEmail, string asunto, string cuerpoHtml)
    {
        var message = new EmailMessage
        {
            From     = _fromAddress,
            Subject  = asunto,
            HtmlBody = cuerpoHtml,
        };
        message.To.Add(destinatarioEmail);
        await _resend.EmailSendAsync(message);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string PlantillaConfirmacionTurno(
        string nombre,
        string sucursal,
        string servicio,
        string profesional,
        DateTime inicio,
        DateTime fin,
        string cancelUrl)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = $"{inicio:HH:mm} – {fin:HH:mm}";

        var cancelBlock = string.IsNullOrWhiteSpace(cancelUrl) ? string.Empty : $"""
                        <table cellpadding="0" cellspacing="0" style="margin-top:24px;border-top:1px solid #e4e4e7;padding-top:20px;width:100%;">
                          <tr>
                            <td>
                              <p style="margin:0 0 12px;font-size:13px;color:#71717a;">¿Necesitás cancelar tu turno?</p>
                              <a href="{cancelUrl}"
                                 style="display:inline-block;padding:11px 24px;background:#fff1f2;color:#b91c1c;font-size:14px;font-weight:600;text-decoration:none;border-radius:8px;border:1.5px solid #fecaca;">
                                Cancelar turno
                              </a>
                              <p style="margin:12px 0 0;font-size:12px;color:#a1a1aa;line-height:1.5;">
                                Este enlace es personal y de un solo uso. Si no querés cancelar, ignorá este mensaje.
                              </p>
                            </td>
                          </tr>
                        </table>
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Turno confirmado</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">¡Tu turno está confirmado!</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, te esperamos con todos los detalles a continuación.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Profesional</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{profesional}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        {cancelBlock}
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                          Este mensaje fue generado automáticamente por MetaTurnos.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaCancelacionStaff(
        string nombre,
        string sucursal,
        string servicio,
        DateTime inicio)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = inicio.ToString("HH:mm");

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Turno cancelado</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">Tu turno fue cancelado</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, lamentamos informarte que tu turno fue cancelado por el equipo de {sucursal}.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0;font-size:14px;color:#52525b;line-height:1.6;">
                          Si querés reprogramar tu turno, no dudes en contactarnos o reservar nuevamente.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">Este mensaje fue generado automáticamente por MetaTurnos.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaCancelacionCliente(
        string nombre,
        string sucursal,
        string servicio,
        DateTime inicio)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = inicio.ToString("HH:mm");

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Cancelación confirmada</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">Cancelación confirmada</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, tu cancelación fue registrada correctamente.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0;font-size:14px;color:#52525b;line-height:1.6;">
                          Esperamos verte pronto. Podés reservar un nuevo turno cuando quieras.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">Este mensaje fue generado automáticamente por MetaTurnos.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaBienvenida(string nombre, string negocio, string urlPanel)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Bienvenido a MetaTurnos</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:32px 40px 28px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="64" height="64" style="display:block;margin:0 auto 12px;object-fit:contain;">
                        <p style="margin:0;font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          MetaTurnos
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:24px;color:#18181b;">¡Bienvenido, {nombre}!</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Tu cuenta de <strong>{negocio}</strong> en MetaTurnos fue creada exitosamente.
                          Seguí estos pasos para empezar a recibir turnos:
                        </p>

                        <!-- Paso 1 -->
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;margin-bottom:12px;">
                          <tr>
                            <td style="padding:18px 20px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td width="36" valign="top" style="font-size:22px;padding-right:12px;">1</td>
                                  <td>
                                    <strong style="font-size:14px;color:#18181b;">Completá el perfil de tu negocio</strong><br>
                                    <span style="font-size:13px;color:#52525b;">Agregá teléfono, dirección y foto para que tus clientes te encuentren.</span><br>
                                    <a href="{urlPanel}/admin/ajustes" style="font-size:13px;color:#2F7D6B;font-weight:600;text-decoration:none;">Ir a Ajustes →</a>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>

                        <!-- Paso 2 -->
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;margin-bottom:12px;">
                          <tr>
                            <td style="padding:18px 20px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td width="36" valign="top" style="font-size:22px;padding-right:12px;">2</td>
                                  <td>
                                    <strong style="font-size:14px;color:#18181b;">Creá tu primer servicio</strong><br>
                                    <span style="font-size:13px;color:#52525b;">Definí los servicios que ofrecés, con duración y precio.</span><br>
                                    <a href="{urlPanel}/admin/servicios" style="font-size:13px;color:#2F7D6B;font-weight:600;text-decoration:none;">Ir a Servicios →</a>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>

                        <!-- Paso 3 -->
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:18px 20px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td width="36" valign="top" style="font-size:22px;padding-right:12px;">3</td>
                                  <td>
                                    <strong style="font-size:14px;color:#18181b;">Invitá a tu equipo</strong><br>
                                    <span style="font-size:13px;color:#52525b;">Sumá profesionales y configurá sus horarios de trabajo.</span><br>
                                    <a href="{urlPanel}/admin/equipo" style="font-size:13px;color:#2F7D6B;font-weight:600;text-decoration:none;">Ir a Equipo →</a>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>

                        <!-- CTA -->
                        <table cellpadding="0" cellspacing="0">
                          <tr>
                            <td style="border-radius:8px;background:#2F7D6B;">
                              <a href="{urlPanel}/admin"
                                 style="display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;">
                                Ir al panel de administración
                              </a>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                          MetaTurnos · Si no creaste esta cuenta podés ignorar este email.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaNuevoTurnoStaff(
        string nombre,
        string sucursal,
        string servicio,
        string clienteNombre,
        DateTime inicio,
        DateTime fin,
        string? observaciones = null)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = $"{inicio:HH:mm} – {fin:HH:mm}";
        var obsHtml  = string.IsNullOrWhiteSpace(observaciones) ? "" : $"""
                                <tr>
                                  <td style="padding:8px 0;border-top:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Mensaje del cliente</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{System.Net.WebUtility.HtmlEncode(observaciones)}</strong>
                                  </td>
                                </tr>
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Nuevo turno reservado</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">¡Nuevo turno reservado!</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, <strong>{clienteNombre}</strong> acaba de reservar un turno en tu agenda.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Cliente</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{clienteNombre}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                                {obsHtml}
                              </table>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0;font-size:14px;color:#52525b;line-height:1.6;">
                          Podés ver los detalles completos en el panel de administración.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">Este mensaje fue generado automáticamente por MetaTurnos.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaAlertaLiberacion(
        string nombre,
        string sucursal,
        string servicio,
        string clienteNombre,
        DateTime inicio)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = inicio.ToString("HH:mm");

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Horario liberado en agenda</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">Horario liberado en tu agenda</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, el cliente <strong>{clienteNombre}</strong> canceló su turno. El horario quedó disponible.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario liberado</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0;font-size:14px;color:#52525b;line-height:1.6;">
                          Este horario ya está disponible para nuevas reservas.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">Este mensaje fue generado automáticamente por MetaTurnos.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaRecordatorio(
        string nombre,
        string sucursal,
        string servicio,
        string profesional,
        DateTime inicio,
        DateTime fin,
        string cancelUrl,
        bool es24h)
    {
        var cultura    = new System.Globalization.CultureInfo("es-AR");
        var fechaStr   = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", cultura);
        var horaStr    = $"{inicio:HH:mm} – {fin:HH:mm}";
        var badge      = es24h ? "Recordatorio — mañana" : "Recordatorio — en 3 horas";
        var badgeColor = es24h ? "#6366f1" : "#f59e0b";
        var intro      = es24h
            ? $"Hola <strong>{nombre}</strong>, te recordamos que mañana tenés un turno reservado."
            : $"Hola <strong>{nombre}</strong>, en menos de 3 horas tenés tu turno. ¡No lo olvides!";

        var cancelBlock = string.IsNullOrWhiteSpace(cancelUrl) ? string.Empty : $"""
                        <table cellpadding="0" cellspacing="0" style="margin-top:24px;border-top:1px solid #e4e4e7;padding-top:20px;width:100%;">
                          <tr>
                            <td>
                              <p style="margin:0 0 12px;font-size:13px;color:#71717a;">¿Necesitás cancelar tu turno?</p>
                              <a href="{cancelUrl}"
                                 style="display:inline-block;padding:11px 24px;background:#fff1f2;color:#b91c1c;font-size:14px;font-weight:600;text-decoration:none;border-radius:8px;border:1.5px solid #fecaca;">
                                Cancelar turno
                              </a>
                              <p style="margin:12px 0 0;font-size:12px;color:#a1a1aa;line-height:1.5;">
                                Este enlace es personal y de un solo uso. Si no querés cancelar, ignorá este mensaje.
                              </p>
                            </td>
                          </tr>
                        </table>
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Recordatorio de turno</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:24px 40px 20px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="56" height="56" style="display:block;margin:0 auto 8px;object-fit:contain;">
                        <p style="margin:0;font-size:18px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          {sucursal}
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <p style="margin:0 0 20px;display:inline-block;">
                          <span style="background:{badgeColor};color:#ffffff;font-size:12px;font-weight:700;letter-spacing:.5px;padding:4px 12px;border-radius:999px;text-transform:uppercase;">
                            {badge}
                          </span>
                        </p>
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">No olvides tu turno</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">{intro}</p>
                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="background:#fafafa;border:1px solid #e4e4e7;border-radius:10px;margin-bottom:28px;">
                          <tr>
                            <td style="padding:24px 28px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Servicio</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{servicio}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Profesional</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{profesional}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;border-bottom:1px solid #f0f0f0;">
                                    <span style="font-size:13px;color:#71717a;">Fecha</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{fechaStr}</strong>
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding:8px 0;">
                                    <span style="font-size:13px;color:#71717a;">Horario</span><br>
                                    <strong style="font-size:15px;color:#18181b;">{horaStr}</strong>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        {cancelBlock}
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                          Este recordatorio fue generado automáticamente por MetaTurnos.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaInvitacion(
        string nombre,
        string sucursal,
        string link,
        bool esReenvio)
    {
        var titulo = esReenvio ? "Nuevo enlace de invitación" : $"¡Hola, {nombre}!";
        var intro  = esReenvio
            ? $"Te enviamos un nuevo enlace de acceso para <strong>{sucursal}</strong>. El enlace anterior ya no es válido."
            : $"Fuiste invitado/a a unirte al equipo de <strong>{sucursal}</strong> como miembro del panel de administración.";

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Invitación TurneroApp</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#2F7D6B;padding:28px 40px;text-align:center;">
                        <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="80" height="80" style="display:block;margin:0 auto 12px;object-fit:contain;">
                        <p style="margin:0;font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                          MetaTurnos
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 12px;font-size:20px;color:#18181b;">{titulo}</h2>
                        <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">{intro}</p>
                        <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hacé clic en el botón para establecer tu contraseña y acceder al panel.
                          El enlace es válido por <strong>7 días</strong>.
                        </p>
                        <table cellpadding="0" cellspacing="0">
                          <tr>
                            <td style="border-radius:8px;background:#2F7D6B;">
                              <a href="{link}"
                                 style="display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;">
                                Aceptar invitación
                              </a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:28px 0 0;font-size:13px;color:#a1a1aa;">
                          Si el botón no funciona, copiá y pegá este enlace en tu navegador:<br>
                          <a href="{link}" style="color:#2F7D6B;word-break:break-all;">{link}</a>
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                          Si no esperabas esta invitación podés ignorar este mensaje.
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlantillaInvitacionExistente(string nombre, string sucursal, string link) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="UTF-8">
          <link rel="preconnect" href="https://fonts.googleapis.com">
          <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Invitación TurneroApp</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
            <tr><td align="center">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                <tr>
                  <td style="background:#2F7D6B;padding:28px 40px;text-align:center;">
                    <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="80" height="80" style="display:block;margin:0 auto 12px;object-fit:contain;">
                    <p style="margin:0;font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                      MetaTurnos
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:40px;">
                    <h2 style="margin:0 0 12px;font-size:20px;color:#18181b;">¡Hola, {nombre}!</h2>
                    <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">
                      Tu correo ya está registrado en MetaTurnos. Solo necesitás aceptar la invitación para
                      unirte a <strong>{sucursal}</strong> con tu cuenta existente.
                    </p>
                    <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">
                      No necesitás crear una nueva contraseña — accedé con la que ya tenés.
                    </p>
                    <table cellpadding="0" cellspacing="0">
                      <tr>
                        <td style="border-radius:8px;background:#2F7D6B;">
                          <a href="{link}"
                             style="display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;">
                            Aceptar invitación
                          </a>
                        </td>
                      </tr>
                    </table>
                    <p style="margin:28px 0 0;font-size:13px;color:#a1a1aa;">
                      Si el botón no funciona, copiá y pegá este enlace en tu navegador:<br>
                      <a href="{link}" style="color:#2F7D6B;word-break:break-all;">{link}</a>
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                    <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                      Si no esperabas esta invitación podés ignorar este mensaje.
                    </p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string PlantillaRecuperacionContrasena(string nombre, string link) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="UTF-8">
          <link rel="preconnect" href="https://fonts.googleapis.com">
          <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;600;700&family=Nunito:wght@400;600&display=swap" rel="stylesheet">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Recuperación de contraseña</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:'DM Sans','Nunito',system-ui,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
            <tr><td align="center">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                <tr>
                  <td style="background:#2F7D6B;padding:28px 40px;text-align:center;">
                    <img src="https://metaturnos.com/logo.png" alt="MetaTurnos" width="80" height="80" style="display:block;margin:0 auto 12px;object-fit:contain;">
                    <p style="margin:0;font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;font-family:'DM Sans',sans-serif;">
                      MetaTurnos
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:40px;">
                    <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">Recuperá tu contraseña</h2>
                    <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                      Hola <strong>{nombre}</strong>, recibimos una solicitud para restablecer la contraseña de tu cuenta.
                      Hacé clic en el botón de abajo para crear una nueva. El enlace expira en <strong>1 hora</strong>.
                    </p>
                    <table cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                      <tr>
                        <td style="border-radius:8px;background:#2F7D6B;">
                          <a href="{link}"
                             style="display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;">
                            Restablecer contraseña
                          </a>
                        </td>
                      </tr>
                    </table>
                    <p style="margin:0 0 8px;font-size:13px;color:#a1a1aa;">
                      Si el botón no funciona, copiá y pegá este enlace en tu navegador:<br>
                      <a href="{link}" style="color:#2F7D6B;word-break:break-all;">{link}</a>
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                    <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                      Si no solicitaste este cambio, podés ignorar este mensaje. Tu contraseña no será modificada.
                    </p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
