namespace TurneroApp.Application.DTOs.Auth;

public class OpcionSucursalDto
{
    public Guid UsuarioId { get; set; }
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool EsOwner { get; set; }
}
