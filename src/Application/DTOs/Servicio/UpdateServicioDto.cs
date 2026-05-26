using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Servicio;

public class UpdateServicioDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Aclaraciones { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int DuracionClienteMin { get; set; }
    public int DuracionAgendaMin { get; set; }
    public bool RequiereSena { get; set; }
    public TipoSena? TipoSena { get; set; }
    public decimal? ValorSena { get; set; }
    public ModalidadPrestacion ModalidadPrestacion { get; set; }
    public TipoServicio TipoServicio { get; set; }
    // Activo se gestiona con DELETE /api/servicios/{id} (DesactivarAsync), no aquí
    /// <summary>Días de la semana en que se ofrece el servicio (0=Dom…6=Sáb). Reemplaza completamente la lista existente.</summary>
    public List<DayOfWeek>? DiasDisponibles { get; set; }
}
