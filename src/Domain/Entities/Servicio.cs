using TurneroApp.Domain.Enums;

namespace TurneroApp.Domain.Entities;

public class Servicio
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
    public bool Activo { get; set; } = true;

    /// <summary>Token de concurrencia optimista. Mapeado a la columna xmin de PostgreSQL (auto-incrementa en cada UPDATE).</summary>
    public uint RowVersion { get; set; }

    // Navigation properties
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
}
