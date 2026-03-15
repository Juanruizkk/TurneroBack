using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public Task EnviarInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        var asunto = $"Tu invitación para unirte a {nombreSucursal}";
        var cuerpo = PlantillaInvitacion(destinatarioNombre, nombreSucursal, linkInvitacion, esReenvio: false);
        return EnviarAsync(destinatarioEmail, destinatarioNombre, asunto, cuerpo);
    }

    public Task EnviarReenvioInvitacionAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string linkInvitacion)
    {
        var asunto = $"Nuevo enlace de invitación — {nombreSucursal}";
        var cuerpo = PlantillaInvitacion(destinatarioNombre, nombreSucursal, linkInvitacion, esReenvio: true);
        return EnviarAsync(destinatarioEmail, destinatarioNombre, asunto, cuerpo);
    }

    public Task EnviarConfirmacionTurnoAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string nombreSucursal,
        string servicioNombre,
        string profesionalNombre,
        DateTime fechaHoraInicio,
        DateTime fechaHoraFin)
    {
        var asunto = $"Turno confirmado en {nombreSucursal} — {fechaHoraInicio:dd/MM/yyyy HH:mm}";
        var cuerpo = PlantillaConfirmacionTurno(destinatarioNombre, nombreSucursal, servicioNombre, profesionalNombre, fechaHoraInicio, fechaHoraFin);
        return EnviarAsync(destinatarioEmail, destinatarioNombre, asunto, cuerpo);
    }

    // ─── Core SMTP ────────────────────────────────────────────────────────────

    private async Task EnviarAsync(
        string destinatarioEmail,
        string destinatarioNombre,
        string asunto,
        string cuerpoHtml)
    {
        var host      = _config["Smtp:Host"]      ?? throw new InvalidOperationException("Smtp:Host no configurado.");
        var port      = int.Parse(_config["Smtp:Port"] ?? "587");
        var username  = _config["Smtp:Username"]  ?? throw new InvalidOperationException("Smtp:Username no configurado.");
        var password  = _config["Smtp:Password"]  ?? throw new InvalidOperationException("Smtp:Password no configurado.");
        var fromEmail = _config["Smtp:FromEmail"] ?? username;
        var fromName  = _config["Smtp:FromName"]  ?? "TurneroApp";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(destinatarioNombre, destinatarioEmail));
        message.Subject = asunto;

        message.Body = new TextPart("html") { Text = cuerpoHtml };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string PlantillaConfirmacionTurno(
        string nombre,
        string sucursal,
        string servicio,
        string profesional,
        DateTime inicio,
        DateTime fin)
    {
        var fechaStr = inicio.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"));
        var horaStr  = $"{inicio:HH:mm} – {fin:HH:mm}";

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Turno confirmado</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">

                    <!-- Header -->
                    <tr>
                      <td style="background:#B4697B;padding:32px 40px;text-align:center;">
                        <p style="margin:0;font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;">
                          ✦ {sucursal}
                        </p>
                      </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 8px;font-size:22px;color:#18181b;">¡Tu turno está confirmado!</h2>
                        <p style="margin:0 0 28px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hola <strong>{nombre}</strong>, te esperamos con todos los detalles a continuación.
                        </p>

                        <!-- Detail card -->
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

                        <p style="margin:0;font-size:13px;color:#a1a1aa;line-height:1.6;">
                          Si necesitás cancelar o reprogramar, contactanos con anticipación.
                        </p>
                      </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                      <td style="background:#fafafa;padding:20px 40px;border-top:1px solid #f0f0f0;">
                        <p style="margin:0;font-size:12px;color:#a1a1aa;text-align:center;">
                          Este mensaje fue generado automáticamente por TurneroApp.
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
        var titulo  = esReenvio ? "Nuevo enlace de invitación" : $"¡Hola, {nombre}!";
        var intro   = esReenvio
            ? $"Te enviamos un nuevo enlace de acceso para <strong>{sucursal}</strong>. El enlace anterior ya no es válido."
            : $"Fuiste invitado/a a unirte al equipo de <strong>{sucursal}</strong> como miembro del panel de administración.";

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Invitación TurneroApp</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:system-ui,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 20px;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">

                    <!-- Header -->
                    <tr>
                      <td style="background:#B4697B;padding:32px 40px;text-align:center;">
                        <p style="margin:0;font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;">
                          ✦ TurneroApp
                        </p>
                      </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                      <td style="padding:40px;">
                        <h2 style="margin:0 0 12px;font-size:20px;color:#18181b;">{titulo}</h2>
                        <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">{intro}</p>
                        <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">
                          Hacé clic en el botón para establecer tu contraseña y acceder al panel.
                          El enlace es válido por <strong>7 días</strong>.
                        </p>

                        <!-- CTA -->
                        <table cellpadding="0" cellspacing="0">
                          <tr>
                            <td style="border-radius:8px;background:#B4697B;">
                              <a href="{link}"
                                 style="display:inline-block;padding:14px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;border-radius:8px;">
                                Aceptar invitación
                              </a>
                            </td>
                          </tr>
                        </table>

                        <p style="margin:28px 0 0;font-size:13px;color:#a1a1aa;">
                          Si el botón no funciona, copiá y pegá este enlace en tu navegador:<br>
                          <a href="{link}" style="color:#B4697B;word-break:break-all;">{link}</a>
                        </p>
                      </td>
                    </tr>

                    <!-- Footer -->
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
}
