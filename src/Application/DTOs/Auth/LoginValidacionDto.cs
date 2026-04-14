namespace TurneroApp.Application.DTOs.Auth;

/// <summary>
/// Resultado intermedio de ValidarAsync. Contiene o bien el usuario autenticado
/// (caso 1 sucursal) o la información para el paso de selección (multi-sucursal).
/// </summary>
public class LoginValidacionDto
{
    /// <summary>Presente cuando hay exactamente 1 sucursal. El controller genera el JWT a partir de este.</summary>
    public UsuarioAutenticadoDto? Usuario { get; set; }

    /// <summary>True cuando el email está asociado a 2+ sucursales activas.</summary>
    public bool RequiereSeleccion { get; set; }

    public string? SeleccionToken { get; set; }
    public List<OpcionSucursalDto>? Opciones { get; set; }
}
