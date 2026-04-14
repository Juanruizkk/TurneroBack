using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.DTOs.SuperAdmin;

public class CrearPlanDto
{
    [Required][MaxLength(100)] public string  Nombre        { get; set; } = string.Empty;
    [MaxLength(300)]           public string? Descripcion   { get; set; }
    [Range(1, int.MaxValue)]   public int     MaxUsuarios   { get; set; }
                               public int?    MaxTurnosMes  { get; set; }
    [Range(0, 9999999)]        public decimal PrecioMensual { get; set; }
                               public bool    EsActivo          { get; set; }
    [Range(0, 9999)]           public int     OrdenDisplay      { get; set; }
    [Range(0, 365)]            public int     DuracionTrialDias { get; set; }
                               public bool    TieneReportes     { get; set; }
}
