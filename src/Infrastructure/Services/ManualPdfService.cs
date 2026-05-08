using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TurneroApp.Infrastructure.Services;

/// <summary>
/// Genera el PDF de «Guía de inicio rápido» que se adjunta al email de bienvenida.
/// </summary>
public static class ManualPdfService
{
    static ManualPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GenerarGuiaInicio(string nombreUsuario, string nombreNegocio)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(45, Unit.Point);
                page.MarginTop(0, Unit.Point);
                page.MarginBottom(35, Unit.Point);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor("#374151").LineHeight(1.55f));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, nombreUsuario, nombreNegocio));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("MetaTurnos · Guía de inicio rápido  ").FontSize(8).FontColor("#9CA3AF");
                    t.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
                    t.Span(" / ").FontSize(8).FontColor("#9CA3AF");
                    t.TotalPages().FontSize(8).FontColor("#9CA3AF");
                });
            });
        })
        .GeneratePdf();
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container)
    {
        container
            .Background("#2F7D6B")
            .PaddingHorizontal(0)
            .PaddingVertical(28, Unit.Point)
            .Column(col =>
            {
                col.Item().AlignCenter().Text("MetaTurnos")
                    .FontSize(28).Bold().FontColor("#FFFFFF");
                col.Item().PaddingTop(4).AlignCenter().Text("Guía de inicio rápido")
                    .FontSize(14).FontColor("#C8EDE6");
            });
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, string nombre, string negocio)
    {
        container.PaddingTop(24, Unit.Point).Column(col =>
        {
            col.Spacing(14);

            // Bienvenida
            col.Item().Column(c =>
            {
                c.Item().Text($"¡Hola, {nombre}!")
                    .FontSize(15).Bold().FontColor("#1F2937");
                c.Item().PaddingTop(6).Text(
                    $"Tu cuenta para «{negocio}» está lista. " +
                    "En esta guía encontrás los pasos esenciales para empezar a recibir turnos " +
                    "en minutos. Para el manual completo, ingresá al panel y hacé clic en " +
                    "Manual de usuario en el menú lateral izquierdo.")
                    .FontSize(10).FontColor("#4B5563");
            });

            Divider(col);

            // 1 — Perfil
            SectionTitle(col, "1", "Configurá tu perfil");
            col.Item().Column(c =>
            {
                Step(c, "Menú lateral → Ajustes → Perfil del negocio",
                    "Completá el nombre, slogan, logo e imagen de fondo. Esta información aparece en tu página pública de reservas.");
                Step(c, "Ajustes → Contacto y ubicación",
                    "Ingresá tu dirección, teléfono y el enlace de Google Maps para que los clientes te encuentren fácilmente.");
                Step(c, "Ajustes → Horarios de atención",
                    "Definí los días y franjas horarias en que tu negocio acepta turnos. Los clientes solo podrán reservar dentro de esos horarios.");
            });

            Divider(col);

            // 2 — Servicios
            SectionTitle(col, "2", "Cargá tus servicios");
            col.Item().Column(c =>
            {
                Step(c, "Menú lateral → Servicios → + Nuevo servicio",
                    "Agregá cada servicio con nombre, precio, duración y modalidad: Presencial, A domicilio u Online.");
                Step(c, "Modalidad «A domicilio»",
                    "Cuando un cliente reserva un servicio a domicilio, el sistema le pide su dirección. " +
                    "Ese mensaje te llega por email, notificación en tiempo real y en el detalle del turno.");
                Step(c, "Senas (opcional)",
                    "Activá «Requiere seña» en un servicio para cobrar un anticipo al momento de la reserva. " +
                    "Para habilitarlo necesitás conectar tu cuenta de Mercado Pago en Ajustes → Mercado Pago.");
            });

            Divider(col);

            // 3 — URL pública
            SectionTitle(col, "3", "Compartí tu URL pública");
            col.Item().Column(c =>
            {
                Step(c, "Ajustes → URL pública",
                    "Tu página de reservas está en metaturnos.com/tu-slug. " +
                    "Personalizá el slug y compartí el enlace en redes sociales, WhatsApp o tu sitio web.");
                Step(c, "Sin registro para el cliente",
                    "Cualquier persona puede reservar desde tu página pública sin necesidad de crear una cuenta. " +
                    "Recibe confirmación por email con su link de gestión personal para cancelar si lo necesita.");
            });

            Divider(col);

            // 4 — Agenda
            SectionTitle(col, "4", "Gestioná tu agenda");
            col.Item().Column(c =>
            {
                Step(c, "Menú lateral → Agenda",
                    "Visualizá todos los turnos en vista diaria, semanal o mensual. " +
                    "La agenda se actualiza en tiempo real cuando llega una nueva reserva.");
                Step(c, "Detalle del turno",
                    "Hacé clic en cualquier turno para ver sus datos, cambiar su estado " +
                    "(Confirmar, Completar, Cancelar) y registrar pagos presenciales.");
                Step(c, "Notificaciones",
                    "Cada vez que un cliente reserva o cancela recibís una notificación " +
                    "en la campanita del panel y un email de aviso.");
            });

            Divider(col);

            // Tip final
            col.Item()
                .Background("#E8F4F1")
                .Border(1).BorderColor("#B8DACE")
                .Padding(14, Unit.Point)
                .Column(c =>
                {
                    c.Item().Text("Consejo")
                        .FontSize(10).Bold().FontColor("#2F7D6B");
                    c.Item().PaddingTop(4).Text(
                        "El manual completo siempre está disponible en el menú lateral del panel " +
                        "de administración. Hacé clic en «Manual de usuario» para abrirlo en cualquier momento.")
                        .FontSize(9.5f).FontColor("#1a5c4d");
                });
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SectionTitle(ColumnDescriptor col, string num, string title)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(26, Unit.Point)
               .Background("#2F7D6B")
               .Height(26, Unit.Point)
               .PaddingTop(5, Unit.Point)
               .AlignCenter()
               .Text(num).FontSize(11).Bold().FontColor("#FFFFFF");

            row.RelativeItem()
               .PaddingLeft(10, Unit.Point)
               .AlignMiddle()
               .Text(title).FontSize(12).Bold().FontColor("#1F2937");
        });
    }

    private static void Step(ColumnDescriptor col, string titulo, string descripcion)
    {
        col.Item().PaddingLeft(36, Unit.Point).PaddingTop(5).Column(c =>
        {
            c.Item().Text($"→  {titulo}").FontSize(10).SemiBold().FontColor("#2F7D6B");
            c.Item().PaddingTop(2).Text(descripcion).FontSize(9.5f).FontColor("#4B5563");
        });
    }

    private static void Divider(ColumnDescriptor col)
    {
        col.Item().PaddingVertical(2).LineHorizontal(1).LineColor("#E5E7EB");
    }
}
