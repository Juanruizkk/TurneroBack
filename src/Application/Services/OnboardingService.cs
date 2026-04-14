using TurneroApp.Application.DTOs.Onboarding;
using TurneroApp.Application.Interfaces;

namespace TurneroApp.Application.Services;

public class OnboardingService
{
    private readonly ISucursalRepository             _sucursalRepo;
    private readonly IServicioRepository             _servicioRepo;
    private readonly IUsuarioSucursalRolRepository   _usrRepo;

    public OnboardingService(
        ISucursalRepository           sucursalRepo,
        IServicioRepository           servicioRepo,
        IUsuarioSucursalRolRepository usrRepo)
    {
        _sucursalRepo = sucursalRepo;
        _servicioRepo = servicioRepo;
        _usrRepo      = usrRepo;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(Guid sucursalId)
    {
        var sucursal       = await _sucursalRepo.GetByIdAsync(sucursalId);
        var serviciosCount = await _servicioRepo.ContarActivosBySucursalAsync(sucursalId);
        var miembros       = await _usrRepo.GetBySucursalAsync(sucursalId);

        var perfilCompleto  = sucursal is not null
            && !string.IsNullOrWhiteSpace(sucursal.Telefono)
            && !string.IsNullOrWhiteSpace(sucursal.Direccion);

        var tieneServicio     = serviciosCount > 0;
        var tieneProfesional  = miembros.Any(m => !m.EsOwner);

        return new OnboardingStatusDto
        {
            PerfilCompleto   = perfilCompleto,
            TieneServicio    = tieneServicio,
            TieneProfesional = tieneProfesional,
        };
    }
}
