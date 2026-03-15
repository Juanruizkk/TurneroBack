using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class CrearSucursalConOwnerDto
{
    // ─── Datos de la sucursal ─────────────────────────────────────────────────

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    // ─── Datos del owner inicial ──────────────────────────────────────────────

    [Required]
    [MaxLength(100)]
    public string OwnerNombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string OwnerApellido { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string OwnerEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string OwnerTelefono { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string OwnerPassword { get; set; } = string.Empty;
}
