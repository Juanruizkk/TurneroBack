namespace TurneroApp.Api.Options;

/// <summary>
/// Configuración de la demo pública por visitante (sección "Demo" de appsettings / env).
/// </summary>
public class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Habilita los endpoints /api/demo/* y el background service de limpieza.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>TTL de cada demo en minutos.</summary>
    public int TtlMinutos { get; set; } = 30;

    /// <summary>Máximo de demos activas por IP.</summary>
    public int MaxPorIp { get; set; } = 3;

    /// <summary>Máximo de demos activas en toda la plataforma.</summary>
    public int MaxGlobal { get; set; } = 100;

    /// <summary>Cada cuántos minutos corre la limpieza de demos vencidas.</summary>
    public int LimpiezaCadaMinutos { get; set; } = 5;
}
