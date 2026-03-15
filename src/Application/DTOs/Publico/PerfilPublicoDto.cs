using TurneroApp.Application.DTOs.Ajustes;

namespace TurneroApp.Application.DTOs.Publico;

public class PerfilPublicoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public string? FotoUrl { get; set; }
    public List<string> Rubros { get; set; } = new();
    public string Moneda { get; set; } = "ARS";
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public int DiasMaximosReservaFutura { get; set; } = 30;

    /// <summary>Horarios de atención por día (del mismo formato que AjustesSucursal).</summary>
    public Dictionary<string, HorarioDiaDto> HorariosAtencion { get; set; } = new();
}
