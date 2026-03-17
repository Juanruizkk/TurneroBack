using System.ComponentModel.DataAnnotations;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.DTOs.Caja;

/// <summary>
/// DTO de entrada para registrar un movimiento en caja.
/// El UsuarioId se extrae del JWT — no se acepta del cliente.
/// </summary>
public class RegistrarMovimientoDto
{
    /// <summary>
    /// Turno asociado. Obligatorio cuando TipoMovimiento == CobroTurno.
    /// </summary>
    public Guid? TurnoId { get; set; }

    [Required]
    public TipoMovimientoCaja TipoMovimiento { get; set; }

    [Required]
    public MedioPago MedioPago { get; set; }

    [Required]
    [Range(0.01, 9_999_999.99, ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    /// <summary>
    /// Concepto del pago al turno. Obligatorio cuando TipoMovimiento == CobroTurno.
    /// </summary>
    public ConceptoPagoTurno? ConceptoPago { get; set; }

    [MaxLength(1000)]
    public string? Observaciones { get; set; }
}
