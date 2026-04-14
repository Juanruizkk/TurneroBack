namespace TurneroApp.Application.DTOs.SuperAdmin;

public class SuscripcionSuperAdminDto
{
    public Guid   SucursalId      { get; set; }
    public string SucursalNombre  { get; set; } = string.Empty;
    public string SucursalSlug    { get; set; } = string.Empty;
    public Guid   PlanId          { get; set; }
    public string PlanNombre      { get; set; } = string.Empty;
    public string Estado          { get; set; } = string.Empty;
    public bool   EnPeriodoPrueba { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int    DiasRestantes   { get; set; }
}
