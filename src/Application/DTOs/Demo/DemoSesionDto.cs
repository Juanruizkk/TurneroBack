namespace TurneroApp.Application.DTOs.Demo;

/// <summary>Respuesta de POST /api/demo/crear: sesión demo con los 4 roles + tokens.</summary>
public class DemoSesionDto
{
    public Guid SucursalId { get; set; }
    public DateTime ExpiraEn { get; set; }
    public List<DemoUsuarioDto> Usuarios { get; set; } = [];
}
