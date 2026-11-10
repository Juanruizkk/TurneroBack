using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.Soporte;

/// <summary>Reporte de problema enviado desde el footer público.</summary>
public class ReporteProblemaDto
{
    [Required(ErrorMessage = "Contanos qué pasó.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 2000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Email de contacto opcional para poder responder.</summary>
    [EmailAddress(ErrorMessage = "El email de contacto no es válido.")]
    [StringLength(254)]
    public string? EmailContacto { get; set; }

    /// <summary>URL donde ocurrió el problema (la setea el frontend).</summary>
    [StringLength(500)]
    public string? UrlOrigen { get; set; }
}
