namespace TurneroApp.Application.DTOs.Onboarding;

public class OnboardingStatusDto
{
    /// <summary>Sucursal tiene Teléfono y Dirección completos.</summary>
    public bool PerfilCompleto { get; set; }

    /// <summary>Existe al menos 1 servicio activo.</summary>
    public bool TieneServicio { get; set; }

    /// <summary>Existe al menos 1 miembro del equipo (no owner).</summary>
    public bool TieneProfesional { get; set; }

    /// <summary>Todos los pasos están completos.</summary>
    public bool Completado => PerfilCompleto && TieneServicio && TieneProfesional;
}
