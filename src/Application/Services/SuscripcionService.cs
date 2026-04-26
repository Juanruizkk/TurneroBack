using TurneroApp.Application.DTOs.Suscripcion;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class SuscripcionService
{
    private readonly ISuscripcionRepository          _repo;
    private readonly ISucursalRepository             _sucursalRepo;
    private readonly IUsuarioSucursalRolRepository   _usrRepo;
    private readonly ITurnoRepository                _turnoRepo;

    public SuscripcionService(
        ISuscripcionRepository        repo,
        ISucursalRepository           sucursalRepo,
        IUsuarioSucursalRolRepository usrRepo,
        ITurnoRepository              turnoRepo)
    {
        _repo         = repo;
        _sucursalRepo = sucursalRepo;
        _usrRepo      = usrRepo;
        _turnoRepo    = turnoRepo;
    }

    public async Task<SuscripcionResponseDto> ObtenerAsync(Guid sucursalId)
    {
        var suscripcion = await _repo.GetBySucursalAsync(sucursalId);

        // Sucursal sin suscripción (creada antes del feature) → auto-crear una retroactiva
        if (suscripcion is null)
        {
            var planBasico = await _repo.GetPlanBasicoAsync();
            if (planBasico is null)
            {
                // No hay planes en la DB: retornar permisivo para no bloquear nada
                return new SuscripcionResponseDto
                {
                    Id               = Guid.Empty,
                    PlanNombre       = "Sin plan",
                    PlanPrecio       = 0,
                    MaxUsuarios      = 999,
                    MaxTurnosMes     = null,
                    Estado           = "SinSuscripcion",
                    EnPeriodoPrueba  = false,
                    FechaVencimiento = DateTime.UtcNow,
                    DiasRestantes    = 0,
                };
            }

            // Usar FechaCreacion de la sucursal como inicio retroactivo
            var sucursal = await _sucursalRepo.GetByIdAsync(sucursalId);
            var fechaInicio = sucursal?.FechaCreacion ?? DateTime.UtcNow;

            // Las sucursales existentes arrancan como Activa con 1 año de gracia
            // (ya usaban el sistema antes de que se implementaran las suscripciones)
            var nueva = new SucursalSuscripcion
            {
                Id               = Guid.NewGuid(),
                SucursalId       = sucursalId,
                PlanId           = planBasico.Id,
                Estado           = EstadoSuscripcion.Activa,
                FechaInicio      = fechaInicio,
                FechaVencimiento = DateTime.UtcNow.AddYears(1),
                EnPeriodoPrueba  = false,
            };

            await _repo.AddAsync(nueva);
            await _repo.SaveChangesAsync();

            // Recargar con el Plan incluido
            suscripcion = await _repo.GetBySucursalAsync(sucursalId);
            if (suscripcion is null) suscripcion = nueva;
            if (suscripcion.Plan is null) suscripcion.Plan = planBasico;
        }

        // Transición de estado lazy
        var ahora = DateTime.UtcNow;
        // La suscripción fue cargada con tracking → solo asignar y guardar; NO llamar a Update()
        // para no marcar el grafo completo (incluyendo Plan) como Modified.
        if (suscripcion.Estado == EstadoSuscripcion.PruebActiva && ahora > suscripcion.FechaVencimiento)
        {
            suscripcion.Estado = EstadoSuscripcion.PruebVencida;
            await _repo.SaveChangesAsync();
        }
        else if (suscripcion.Estado == EstadoSuscripcion.Activa && ahora > suscripcion.FechaVencimiento)
        {
            suscripcion.Estado = EstadoSuscripcion.Vencida;
            await _repo.SaveChangesAsync();
        }

        var diasRestantes    = (int)Math.Max(0, Math.Ceiling((suscripcion.FechaVencimiento - ahora).TotalDays));
        var usuariosActuales = await _usrRepo.ContarActivosBySucursalAsync(sucursalId);
        var turnosMes        = await _turnoRepo.ContarPorSucursalEnMesAsync(sucursalId, ahora.Year, ahora.Month);

        return MapToResponse(suscripcion, diasRestantes, usuariosActuales, turnosMes);
    }

    private static SuscripcionResponseDto MapToResponse(
        Domain.Entities.SucursalSuscripcion s,
        int diasRestantes,
        int usuariosActuales,
        int turnosMes) => new()
    {
        Id               = s.Id,
        PlanNombre       = s.Plan.Nombre,
        PlanPrecio       = s.Plan.PrecioMensual,
        MaxUsuarios      = s.Plan.MaxUsuarios,
        MaxTurnosMes     = s.Plan.MaxTurnosMes,
        Estado           = s.Estado.ToString(),
        EnPeriodoPrueba  = s.EnPeriodoPrueba,
        FechaVencimiento = s.FechaVencimiento,
        DiasRestantes    = diasRestantes,
        UsuariosActuales = usuariosActuales,
        TurnosMesActual  = turnosMes,
    };
}
