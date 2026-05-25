using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Servicio;

public class ServicioResponseDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
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
    public bool Activo { get; set; }
    /// <summary>Días de la semana en que se ofrece el servicio. Vacío = todos los días.</summary>
    public List<DayOfWeek> DiasDisponibles { get; set; } = [];
}
