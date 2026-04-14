namespace TurneroApp.Domain.Entities;

public class Plan
{
    public Guid    Id             { get; set; }
    public string  Nombre         { get; set; } = string.Empty;
    public string? Descripcion    { get; set; }
    public int     MaxUsuarios    { get; set; }
    public int?    MaxTurnosMes   { get; set; }   // null = ilimitado
    public decimal PrecioMensual  { get; set; }
    public bool    EsActivo          { get; set; } = true;
    public int     OrdenDisplay      { get; set; }
    public int     DuracionTrialDias { get; set; } = 14;  // cuántos días de prueba otorga este plan
    public bool    TieneReportes     { get; set; } = false; // acceso al módulo de reportes

    public ICollection<SucursalSuscripcion> Suscripciones { get; set; } = [];
}
