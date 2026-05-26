using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Servicio;

public class CreateServicioDto
{
    [Required]
    public Guid SucursalId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Aclaraciones { get; set; } = string.Empty;

    [Required]
    [Range(0, 9999999.99)]
    public decimal Precio { get; set; }

    [Required]
    [Range(1, 1440)]
    public int DuracionClienteMin { get; set; }

    [Required]
    [Range(1, 1440)]
    public int DuracionAgendaMin { get; set; }

    public bool RequiereSena { get; set; }
    public TipoSena? TipoSena { get; set; }

    [Range(0, 9999999.99)]
    public decimal? ValorSena { get; set; }

    public ModalidadPrestacion ModalidadPrestacion { get; set; }
    public TipoServicio TipoServicio { get; set; }
    /// <summary>Días de la semana en que se ofrece el servicio. Debe enviarse con los días explícitos (0=Dom…6=Sáb). El panel pre-selecciona los días abiertos de la sucursal.</summary>
    public List<DayOfWeek>? DiasDisponibles { get; set; }
}
