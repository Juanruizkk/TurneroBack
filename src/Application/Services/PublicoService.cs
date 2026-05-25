using System.Text.Json;
using TurneroApp.Application.DTOs.Ajustes;
using TurneroApp.Application.DTOs.Publico;
using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
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

    private readonly ISucursalRepository           _sucursalRepo;
    private readonly IServicioRepository           _servicioRepo;
    private readonly ITurnoRepository              _turnoRepo;
    private readonly IUsuarioSucursalRolRepository _usrRepo;
    private readonly IHorarioLaboralRepository     _horarioRepo;
    private readonly IUsuarioServicioRepository    _usuarioServicioRepo;
    private readonly IClienteRepository            _clienteRepo;
    private readonly TurnoService                  _turnoService;

    public PublicoService(
        ISucursalRepository           sucursalRepo,
        IServicioRepository           servicioRepo,
        ITurnoRepository              turnoRepo,
        IUsuarioSucursalRolRepository usrRepo,
        IHorarioLaboralRepository     horarioRepo,
        IUsuarioServicioRepository    usuarioServicioRepo,
        IClienteRepository            clienteRepo,
        TurnoService                  turnoService)
    {
        _sucursalRepo        = sucursalRepo;
        _servicioRepo        = servicioRepo;
        _turnoRepo           = turnoRepo;
        _usrRepo             = usrRepo;
        _horarioRepo         = horarioRepo;
        _usuarioServicioRepo = usuarioServicioRepo;
        _clienteRepo         = clienteRepo;
        _turnoService        = turnoService;
    }

    // ─── Perfil público ───────────────────────────────────────────────────────

    public async Task<PerfilPublicoDto> ObtenerPerfilAsync(string slug)
    {
        var sucursal = await _sucursalRepo.GetBySlugAsync(slug)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var horariosJson = sucursal.ConfiguracionSucursal?.HorariosAtencionJson ?? "{}";
        var horarios = JsonSerializer.Deserialize<Dictionary<string, HorarioDiaDto>>(horariosJson, _json) ?? new();

        var config = sucursal.ConfiguracionSucursal;

        return new PerfilPublicoDto
        {
            SucursalId    = sucursal.Id,
            Slug          = sucursal.Slug,
            Nombre        = sucursal.Nombre,
            Slogan        = sucursal.Slogan,
            FotoUrl       = sucursal.FotoUrl,
            BackgroundUrl = sucursal.BackgroundUrl,
            Rubros        = sucursal.Rubros,
            Moneda        = sucursal.Moneda,
            Direccion     = sucursal.Direccion,
            GoogleMapsUrl = sucursal.GoogleMapsUrl,
            Telefono      = sucursal.Telefono,
            Email     = sucursal.Email,
            DiasMaximosReservaFutura = config?.DiasMaximosReservaFutura ?? 30,
            HorariosAtencion = horarios,
        };
    }

    // ─── Slots disponibles ────────────────────────────────────────────────────

    /// <param name="fecha">Fecha en formato yyyy-MM-dd (hora local del negocio).</param>
    public async Task<List<SlotDisponibleDto>> ObtenerSlotsAsync(
        string slug, DateOnly fecha, Guid servicioId)
    {
        // 1. Configuración
        var sucursal = await _sucursalRepo.GetBySlugAsync(slug);
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

        // 3. Duración del servicio + validación de día disponible
        var servicio = await _servicioRepo.GetByIdConDiasAsync(servicioId);
        if (servicio is null) return [];

        if (servicio.DiasDisponibles.Count > 0 &&
            !servicio.DiasDisponibles.Any(d => d.DiaSemana == fecha.DayOfWeek))
            return [];

        int duracion = servicio.DuracionAgendaMin;

        // 4. Usuarios activos que pueden atender turnos (cualquier rol operativo)
        var usrs = await _usrRepo.GetBySucursalAsync(sucursal.Id);
        var todosDict = usrs.ToDictionary(usr => usr.UsuarioId, usr => usr.Usuario);
        var profesionales = usrs
            .Where(usr => usr.Estado == EstadoUsuario.Activo &&
                          (usr.Rol == RolUsuario.Profesional || usr.Rol == RolUsuario.Administrativo))
            .Select(usr => usr.UsuarioId)
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
        var turnosDelDia = (await _turnoRepo.GetActivosSucursalEnRangoAsync(sucursal.Id, diaInicio, diaFin))
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

                    // Filtro por horario laboral (solo si el profesional tiene horarios activos configurados)
                    if (horariosPorProf.TryGetValue(profId, out var horarios) && horarios.Any(h => h.Activo))
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

    // ─── Overloads por sucursalId (para bookings con VITE_SUCURSAL_ID) ──────────

    /// <summary>Perfil público buscando por ID en lugar de slug.</summary>
    public async Task<PerfilPublicoDto?> ObtenerPerfilPorIdAsync(Guid sucursalId)
    {
        var sucursal = await _sucursalRepo.GetByIdConConfiguracionAsync(sucursalId);
        if (sucursal is null) return null;
        return await ObtenerPerfilAsync(sucursal.Slug!);
    }

    /// <summary>Slots disponibles buscando por sucursalId en lugar de slug.</summary>
    public async Task<List<SlotDisponibleDto>> ObtenerSlotsAsync(
        Guid sucursalId, DateOnly fecha, Guid servicioId)
    {
        // Solo necesitamos el slug para reusar el método existente
        var sucursal = await _sucursalRepo.GetByIdAsync(sucursalId);
        if (sucursal is null) return [];
        return await ObtenerSlotsAsync(sucursal.Slug!, fecha, servicioId);
    }

    // ─── Reserva pública (sin autenticación) ──────────────────────────────────

    /// <summary>
    /// Busca o crea el cliente y crea el turno en una sola operación.
    /// Permite que el booking flow funcione sin necesitar un token de autenticación.
    /// </summary>
    public async Task<DTOs.Turno.TurnoResponseDto> ReservarAsync(
        Guid sucursalId, ReservarTurnoPublicoDto dto)
    {
        // Buscar cliente existente por teléfono en la sucursal
        var cliente = await _clienteRepo.BuscarPorTelefonoAsync(sucursalId, dto.Telefono);

        Guid clienteId;
        if (cliente is not null)
        {
            // Actualizar nombre/apellido/email si el cliente ya existe y viene información nueva
            cliente.Nombre   = dto.Nombre.Trim();
            cliente.Apellido = dto.Apellido?.Trim() ?? cliente.Apellido;
            if (!string.IsNullOrWhiteSpace(dto.Email))
                cliente.Email = dto.Email.Trim();
            _clienteRepo.Update(cliente);
            await _clienteRepo.SaveChangesAsync();
            clienteId = cliente.Id;
        }
        else
        {
            var nuevo = new Cliente
            {
                Id         = Guid.NewGuid(),
                SucursalId = sucursalId,
                Nombre     = dto.Nombre.Trim(),
                Apellido   = dto.Apellido?.Trim() ?? "-",
                Telefono   = dto.Telefono.Trim(),
                Email      = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                Activo     = true,
            };
            await _clienteRepo.AddAsync(nuevo);
            await _clienteRepo.SaveChangesAsync();
            clienteId = nuevo.Id;
        }

        var createDto = new CreateTurnoDto
        {
            SucursalId      = sucursalId,
            ClienteId       = clienteId,
            ServicioId      = dto.ServicioId,
            ProfesionalId   = dto.ProfesionalId,
            FechaHoraInicio = dto.FechaHoraInicio,
            Observaciones   = dto.Observaciones,
        };

        return await _turnoService.CreateAsync(createDto, OrigenTurno.Web);
    }
}
