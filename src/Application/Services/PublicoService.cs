using System.Text.Json;
using TurneroApp.Application.DTOs.Ajustes;
using TurneroApp.Application.DTOs.Publico;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class PublicoService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<DayOfWeek, string> _dayNames = new()
    {
        [DayOfWeek.Monday]    = "Lunes",
        [DayOfWeek.Tuesday]   = "Martes",
        [DayOfWeek.Wednesday] = "Miercoles",
        [DayOfWeek.Thursday]  = "Jueves",
        [DayOfWeek.Friday]    = "Viernes",
        [DayOfWeek.Saturday]  = "Sabado",
        [DayOfWeek.Sunday]    = "Domingo",
    };

    private readonly ISucursalRepository        _sucursalRepo;
    private readonly IServicioRepository        _servicioRepo;
    private readonly ITurnoRepository           _turnoRepo;
    private readonly IUsuarioRepository         _usuarioRepo;
    private readonly IHorarioLaboralRepository  _horarioRepo;
    private readonly IUsuarioServicioRepository _usuarioServicioRepo;

    public PublicoService(
        ISucursalRepository        sucursalRepo,
        IServicioRepository        servicioRepo,
        ITurnoRepository           turnoRepo,
        IUsuarioRepository         usuarioRepo,
        IHorarioLaboralRepository  horarioRepo,
        IUsuarioServicioRepository usuarioServicioRepo)
    {
        _sucursalRepo        = sucursalRepo;
        _servicioRepo        = servicioRepo;
        _turnoRepo           = turnoRepo;
        _usuarioRepo         = usuarioRepo;
        _horarioRepo         = horarioRepo;
        _usuarioServicioRepo = usuarioServicioRepo;
    }

    // ─── Perfil público ───────────────────────────────────────────────────────

    public async Task<PerfilPublicoDto> ObtenerPerfilAsync(Guid sucursalId)
    {
        var sucursal = await _sucursalRepo.GetByIdConConfiguracionAsync(sucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var horariosJson = sucursal.ConfiguracionSucursal?.HorariosAtencionJson ?? "{}";
        var horarios = JsonSerializer.Deserialize<Dictionary<string, HorarioDiaDto>>(horariosJson, _json) ?? new();

        var config = sucursal.ConfiguracionSucursal;

        return new PerfilPublicoDto
        {
            Nombre    = sucursal.Nombre,
            Slogan    = sucursal.Slogan,
            FotoUrl   = sucursal.FotoUrl,
            Rubros    = sucursal.Rubros,
            Moneda    = sucursal.Moneda,
            Direccion = sucursal.Direccion,
            Telefono  = sucursal.Telefono,
            Email     = sucursal.Email,
            DiasMaximosReservaFutura = config?.DiasMaximosReservaFutura ?? 30,
            HorariosAtencion = horarios,
        };
    }

    // ─── Slots disponibles ────────────────────────────────────────────────────

    /// <param name="fecha">Fecha en formato yyyy-MM-dd (hora local del negocio).</param>
    public async Task<List<SlotDisponibleDto>> ObtenerSlotsAsync(
        Guid sucursalId, DateOnly fecha, Guid servicioId)
    {
        // 1. Configuración
        var sucursal = await _sucursalRepo.GetByIdConConfiguracionAsync(sucursalId);
        if (sucursal is null) return [];

        var config = sucursal.ConfiguracionSucursal;
        int buffer = config?.MinutosBufferEntreTurnos ?? 0;

        // 2. Horarios del día
        var horariosJson = config?.HorariosAtencionJson;
        Dictionary<string, HorarioDiaDto> horarios;

        if (string.IsNullOrWhiteSpace(horariosJson) || horariosJson == "{}")
        {
            // Sin configuración guardada: usar horario por defecto 09-13 y 15-19 Lun-Vie, 09-13 Sáb
            horarios = AjustesService.HorariosDefault;
        }
        else
        {
            horarios = JsonSerializer.Deserialize<Dictionary<string, HorarioDiaDto>>(horariosJson, _json) ?? AjustesService.HorariosDefault;
        }

        if (!_dayNames.TryGetValue(fecha.DayOfWeek, out var nombreDia)) return [];
        if (!horarios.TryGetValue(nombreDia, out var horarioDia) || !horarioDia.Abierto) return [];
        if (horarioDia.Franjas is not { Count: > 0 }) return [];

        // 3. Duración del servicio
        var servicio = await _servicioRepo.GetByIdAsync(servicioId);
        if (servicio is null) return [];
        int duracion = servicio.DuracionAgendaMin;

        // 4. Usuarios activos que pueden atender turnos (cualquier rol operativo)
        var todos = await _usuarioRepo.GetBySucursalAsync(sucursalId);
        var todosDict = todos.ToDictionary(u => u.Id);
        var profesionales = todos
            .Where(u => u.Estado == EstadoUsuario.Activo &&
                        (u.Rol == RolUsuario.Profesional || u.Rol == RolUsuario.Administrativo))
            .Select(u => u.Id)
            .ToList();

        // Si no hay ningún usuario activo, igualmente generar slots sin verificar conflictos
        bool verificarConflictos = profesionales.Count > 0;
        if (!verificarConflictos)
        {
            // Sin usuarios: generar slots libres sin asignar profesional
            profesionales = [Guid.NewGuid()]; // placeholder
        }

        // 5. Turnos activos del día (timestamp without time zone → Kind=Unspecified)
        var diaInicio = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var diaFin    = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified);
        var turnosDelDia = (await _turnoRepo.GetActivosSucursalEnRangoAsync(sucursalId, diaInicio, diaFin))
            .ToList();

        // Horarios y servicios por profesional (carga en batch — solo para usuarios reales)
        Dictionary<Guid, List<Domain.Entities.HorarioLaboral>> horariosPorProf = new();
        Dictionary<Guid, List<Domain.Entities.UsuarioServicio>> serviciosPorProf = new();
        if (verificarConflictos)
        {
            horariosPorProf  = await _horarioRepo.GetByUsuariosAsync(profesionales);
            serviciosPorProf = await _usuarioServicioRepo.GetByUsuariosAsync(profesionales);
        }

        // 6. Generar slots por franja
        var resultado = new List<SlotDisponibleDto>();
        var now   = DateTime.Now;           // hora local, igual que los slots del horario
        int anticipacionMinutos = config?.MinutosAnticipacionMinimaReserva ?? 60;

        foreach (var franja in horarioDia.Franjas)
        {
            if (!TimeOnly.TryParse(franja.Apertura, out var apertura)) continue;
            if (!TimeOnly.TryParse(franja.Cierre,   out var cierre))   continue;

            var cursor = apertura;
            while (cursor.Add(TimeSpan.FromMinutes(duracion)) <= cierre)
            {
                var slotInicio = fecha.ToDateTime(cursor);
                var slotFin    = slotInicio.AddMinutes(duracion);

                // Filtrar slots dentro de la ventana de anticipación mínima
                if (slotInicio <= now.AddMinutes(anticipacionMinutos))
                {
                    cursor = cursor.Add(TimeSpan.FromMinutes(duracion + buffer));
                    continue;
                }

                // Profesionales libres para este slot (uno por profesional disponible)
                var profesionalesLibres = profesionales.Where(profId =>
                {
                    // Conflicto con turno existente
                    if (turnosDelDia.Any(t =>
                            t.ProfesionalId == profId &&
                            t.FechaHoraInicio < slotFin &&
                            t.FechaHoraFin    > slotInicio)) return false;

                    // Filtro por horario laboral (solo si el profesional tiene horarios configurados)
                    if (horariosPorProf.TryGetValue(profId, out var horarios) && horarios.Count > 0)
                    {
                        var slotHoraInicio = slotInicio.TimeOfDay;
                        var slotHoraFin    = slotFin.TimeOfDay;
                        if (!horarios.Any(h =>
                                h.Activo &&
                                h.DiaSemana == fecha.DayOfWeek &&
                                h.HoraDesde <= slotHoraInicio &&
                                h.HoraHasta >= slotHoraFin))
                            return false;
                    }

                    // Filtro por servicio (solo si el profesional tiene servicios configurados)
                    if (serviciosPorProf.TryGetValue(profId, out var svcs) && svcs.Count > 0)
                        if (!svcs.Any(s => s.ServicioId == servicioId)) return false;

                    return true;
                }).ToList();

                foreach (var profId in profesionalesLibres)
                {
                    var nombreProf = string.Empty;
                    if (verificarConflictos && todosDict.TryGetValue(profId, out var u))
                        nombreProf = $"{u.Nombre} {u.Apellido}".Trim();

                    resultado.Add(new SlotDisponibleDto
                    {
                        Hora              = cursor.ToString("HH:mm"),
                        ProfesionalId     = profId.ToString(),
                        ProfesionalNombre = nombreProf,
                    });
                }

                cursor = cursor.Add(TimeSpan.FromMinutes(duracion + buffer));
            }
        }

        return resultado;
    }
}
