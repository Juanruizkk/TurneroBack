namespace TurneroApp.Application.DTOs.Ajustes;

public class FranjaHorariaDto
{
    public string Apertura { get; set; } = "09:00";
    public string Cierre { get; set; } = "13:00";
}

public class HorarioDiaDto
{
    public bool Abierto { get; set; }
    /// <summary>Lista de franjas horarias del día (ej: mañana 9-13, tarde 15-19).</summary>
    public List<FranjaHorariaDto> Franjas { get; set; } = new();
}
