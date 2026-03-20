using TurneroApp.Application.DTOs.Turno;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class TurnoService
{
    private readonly ITurnoRepository             _turnoRepository;
    private readonly IServicioRepository          _servicioRepository;
    private readonly IClienteRepository           _clienteRepository;
    private readonly ISucursalRepository          _sucursalRepository;
    private readonly IEmailService                _emailService;
    private readonly IComentarioTurnoRepository   _comentarioRepository;
    private readonly string                       _frontendUrl;
    private readonly ITurnoNotificacionService?   _notificaciones;
    private readonly IHorarioLaboralRepository?   _horarioRepo;
    private readonly IUsuarioServicioRepository?  _usuarioServicioRepo;

    public TurnoService(
        ITurnoRepository turnoRepository,
        IServicioRepository servicioRepository,
        IClienteRepository clienteRepository,
        ISucursalRepository sucursalRepository,
        IEmailService emailService,
        IComentarioTurnoRepository comentarioRepository,
        string frontendUrl,
        ITurnoNotificacionService? notificaciones = null,
        IHorarioLaboralRepository? horarioRepo = null,
        IUsuarioServicioRepository? usuarioServicioRepo = null)
    {
        _turnoRepository      = turnoRepository;
        _servicioRepository   = servicioRepository;
        _clienteRepository    = clienteRepository;
        _sucursalRepository   = sucursalRepository;
        _emailService         = emailService;
        _comentarioRepository = comentarioRepository;
        _frontendUrl          = frontendUrl;
        _notificaciones       = notificaciones;
        _horarioRepo          = horarioRepo;
        _usuarioServicioRepo  = usuarioServicioRepo;
    }

    public async Task<IEnumerable<TurnoResponseDto>> GetAllAsync()
    {
        var turnos = await _turnoRepository.GetAllAsync();
        return turnos.Select(MapToResponse);
    }

    public async Task<IEnumerable<TurnoResponseDto>> GetBySucursalAsync(Guid sucursalId)
    {
        var turnos = await _turnoRepository.GetBySucursalAsync(sucursalId);
        return turnos.Select(MapToResponse);
    }

    public async Task<TurnoResponseDto?> GetByIdAsync(Guid id)
    {
        var turno = await _turnoRepository.GetByIdConRelacionesAsync(id);
        return turno is null ? null : MapToResponse(turno);
    }

    public async Task<TurnoResponseDto?> GetByTokenAsync(string token)
    {
        var turno = await _turnoRepository.GetByTokenAsync(token);
        return turno is null ? null : MapToResponse(turno);
    }

    public async Task<TurnoResponseDto> CreateAsync(CreateTurnoDto dto)
    {
        var servicio = await _servicioRepository.GetByIdAsync(dto.ServicioId)
            ?? throw new InvalidOperationException($"Servicio con Id {dto.ServicioId} no encontrado.");

        var cliente = await _clienteRepository.GetByIdAsync(dto.ClienteId)
            ?? throw new InvalidOperationException($"Cliente con Id {dto.ClienteId} no encontrado.");

        var fechaHoraFin = dto.FechaHoraInicio.AddMinutes(servicio.DuracionAgendaMin);

        // Validar horario laboral del profesional (solo si tiene horarios configurados)
        if (_horarioRepo is not null)
        {
            var horarios = await _horarioRepo.GetByUsuarioIdAsync(dto.ProfesionalId);
            if (horarios.Count > 0)
            {
                var ok = horarios.Any(h =>
                    h.Activo &&
                    h.DiaSemana == dto.FechaHoraInicio.DayOfWeek &&
                    h.HoraDesde <= dto.FechaHoraInicio.TimeOfDay &&
                    h.HoraHasta >= fechaHoraFin.TimeOfDay);
                if (!ok)
                    throw new InvalidOperationException(
                        "El profesional no tiene disponibilidad en ese horario.");
            }
        }

        // Validar que el profesional ofrece el servicio (solo si tiene servicios configurados)
        if (_usuarioServicioRepo is not null)
        {
            var serviciosProf = await _usuarioServicioRepo.GetByUsuarioIdAsync(dto.ProfesionalId);
            if (serviciosProf.Count > 0 && !serviciosProf.Any(s => s.ServicioId == dto.ServicioId))
                throw new InvalidOperationException("El profesional no ofrece este servicio.");
        }

        var hayConflicto = await _turnoRepository.ExisteConflictoHorarioAsync(
            dto.ProfesionalId, dto.FechaHoraInicio, fechaHoraFin);

        if (hayConflicto)
        {
            throw new InvalidOperationException(
                "El profesional ya tiene un turno en el horario seleccionado. Por favor elija otro horario.");
        }

        decimal? montoSena = null;
        if (servicio.RequiereSena && servicio.TipoSena.HasValue && servicio.ValorSena.HasValue)
        {
            montoSena = servicio.TipoSena == TipoSena.Fija
                ? servicio.ValorSena.Value
                : Math.Round(servicio.Precio * servicio.ValorSena.Value / 100m, 2);
        }

        // Estado inicial: turnos Online con seña → PendientePago; resto respeta modoConfirmacion
        var sucursalConfig = await _sucursalRepository.GetByIdConConfiguracionAsync(dto.SucursalId);
        var modo = sucursalConfig?.ConfiguracionSucursal?.ModoConfirmacionTurnos
                   ?? ModoConfirmacionTurnos.Automatico;
        EstadoTurno estadoInicial;
        if (dto.Origen == OrigenTurno.Web && servicio.RequiereSena)
            estadoInicial = EstadoTurno.PendientePago;
        else if (dto.Origen == OrigenTurno.Web && modo == ModoConfirmacionTurnos.Manual)
            estadoInicial = EstadoTurno.Pendiente;
        else
            estadoInicial = EstadoTurno.Confirmado;

        var turno = new Turno
        {
            Id = Guid.NewGuid(),
            SucursalId = dto.SucursalId,
            ClienteId = dto.ClienteId,
            ServicioId = dto.ServicioId,
            ProfesionalId = dto.ProfesionalId,
            FechaHoraInicio = dto.FechaHoraInicio,
            FechaHoraFin = fechaHoraFin,
            Estado = estadoInicial,
            Origen = dto.Origen,
            Observaciones = dto.Observaciones,
            TokenGestion = Guid.NewGuid().ToString("N"),
            PrecioServicio = servicio.Precio,
            MontoSena = montoSena,
            TotalCobrado = 0,
            FechaCreacion = DateTime.Now
        };

        await _turnoRepository.AddAsync(turno);
        await _turnoRepository.SaveChangesAsync();

        // Recargar con relaciones para tener Profesional incluido
        var turnoCompleto = await _turnoRepository.GetByIdConRelacionesAsync(turno.Id) ?? turno;
        turnoCompleto.Cliente  = turnoCompleto.Cliente  ?? cliente;
        turnoCompleto.Servicio = turnoCompleto.Servicio ?? servicio;

        // Enviar email de confirmación solo si el turno quedó confirmado (no si está esperando pago)
        if (estadoInicial != EstadoTurno.PendientePago && !string.IsNullOrWhiteSpace(cliente.Email))
        {
            try
            {
                var sucursal = await _sucursalRepository.GetByIdAsync(dto.SucursalId);
                var sucursalNombre    = sucursal?.Nombre ?? "TurneroApp";
                var profesionalNombre = turnoCompleto.Profesional is not null
                    ? $"{turnoCompleto.Profesional.Nombre} {turnoCompleto.Profesional.Apellido}".Trim()
                    : string.Empty;

                var cancelUrl = $"{_frontendUrl.TrimEnd('/')}/turno/{turno.TokenGestion}";
                await _emailService.EnviarConfirmacionTurnoAsync(
                    cliente.Email,
                    $"{cliente.Nombre} {cliente.Apellido}".Trim(),
                    sucursalNombre,
                    servicio.Nombre,
                    profesionalNombre,
                    turno.FechaHoraInicio,
                    turno.FechaHoraFin,
                    cancelUrl);
            }
            catch
            {
                // El email es no-crítico: si falla no se cancela la reserva
            }
        }

        var responseDto = MapToResponse(turnoCompleto);

        // Notificación en tiempo real solo si el turno quedó confirmado de inmediato
        // (si quedó en PendientePago, la notificación se dispara cuando se confirme el pago)
        if (_notificaciones is not null && estadoInicial == EstadoTurno.Confirmado)
        {
            try { await _notificaciones.NotificarNuevoTurnoAsync(responseDto); }
            catch { /* SignalR es no-crítico */ }
        }

        return responseDto;
    }

    public async Task<TurnoResponseDto?> UpdateEstadoAsync(Guid id, UpdateTurnoDto dto)
    {
        var turno = await _turnoRepository.GetByIdConRelacionesAsync(id);
        if (turno is null) return null;

        turno.Estado = dto.Estado;

        if (dto.Observaciones is not null)
            turno.Observaciones = dto.Observaciones;

        if (dto.FechaHoraInicio.HasValue && dto.ProfesionalId.HasValue)
        {
            var servicio = await _servicioRepository.GetByIdAsync(turno.ServicioId);
            if (servicio is not null)
            {
                var nuevaFin = dto.FechaHoraInicio.Value.AddMinutes(servicio.DuracionAgendaMin);
                var profesionalId = dto.ProfesionalId.Value;

                var hayConflicto = await _turnoRepository.ExisteConflictoHorarioAsync(
                    profesionalId, dto.FechaHoraInicio.Value, nuevaFin, id);

                if (hayConflicto)
                    throw new InvalidOperationException(
                        "El profesional ya tiene un turno en el nuevo horario seleccionado.");

                turno.FechaHoraInicio = dto.FechaHoraInicio.Value;
                turno.FechaHoraFin = nuevaFin;
                turno.ProfesionalId = profesionalId;
            }
        }
        else if (dto.FechaHoraInicio.HasValue)
        {
            var servicio = await _servicioRepository.GetByIdAsync(turno.ServicioId);
            if (servicio is not null)
            {
                var nuevaFin = dto.FechaHoraInicio.Value.AddMinutes(servicio.DuracionAgendaMin);
                var hayConflicto = await _turnoRepository.ExisteConflictoHorarioAsync(
                    turno.ProfesionalId, dto.FechaHoraInicio.Value, nuevaFin, id);

                if (hayConflicto)
                    throw new InvalidOperationException(
                        "El profesional ya tiene un turno en el nuevo horario seleccionado.");

                turno.FechaHoraInicio = dto.FechaHoraInicio.Value;
                turno.FechaHoraFin = nuevaFin;
            }
        }

        if (dto.ProfesionalId.HasValue && !dto.FechaHoraInicio.HasValue)
        {
            turno.ProfesionalId = dto.ProfesionalId.Value;
        }

        _turnoRepository.Update(turno);
        await _turnoRepository.SaveChangesAsync();

        // ─── Notificaciones de cancelación ────────────────────────────────────
        var estadoNuevo = dto.Estado;
        if ((estadoNuevo == EstadoTurno.CanceladoStaff || estadoNuevo == EstadoTurno.CanceladoCliente)
            && turno.Cliente is not null
            && !string.IsNullOrWhiteSpace(turno.Cliente.Email))
        {
            var sucursalInfo = await _sucursalRepository.GetByIdAsync(turno.SucursalId);
            var sucursalNombre = sucursalInfo?.Nombre ?? "TurneroApp";
            var servicioNombre = turno.Servicio?.Nombre ?? string.Empty;
            var clienteNombre  = $"{turno.Cliente.Nombre} {turno.Cliente.Apellido}".Trim();
            var clienteEmail   = turno.Cliente.Email;

            try
            {
                if (estadoNuevo == EstadoTurno.CanceladoStaff)
                {
                    await _emailService.EnviarCancelacionStaffAsync(
                        clienteEmail, clienteNombre, sucursalNombre, servicioNombre, turno.FechaHoraInicio);
                }
                else // CanceladoCliente
                {
                    await _emailService.EnviarCancelacionClienteAsync(
                        clienteEmail, clienteNombre, sucursalNombre, servicioNombre, turno.FechaHoraInicio);

                    // Alerta al profesional (o email de sucursal como fallback)
                    var profesional = turno.Profesional;
                    var alertaEmail = !string.IsNullOrWhiteSpace(profesional?.Email)
                        ? profesional!.Email
                        : sucursalInfo?.Email;

                    if (!string.IsNullOrWhiteSpace(alertaEmail))
                    {
                        var alertaNombre = profesional is not null
                            ? $"{profesional.Nombre} {profesional.Apellido}".Trim()
                            : sucursalNombre;

                        await _emailService.EnviarAlertaLiberacionAgendaAsync(
                            alertaEmail, alertaNombre, sucursalNombre, servicioNombre,
                            clienteNombre, turno.FechaHoraInicio);
                    }
                }
            }
            catch
            {
                // El email es no-crítico
            }
        }

        var turnoDto = MapToResponse(turno);

        // Notificación en tiempo real para cancelaciones
        if (_notificaciones is not null
            && (estadoNuevo == EstadoTurno.CanceladoStaff || estadoNuevo == EstadoTurno.CanceladoCliente))
        {
            try { await _notificaciones.NotificarCancelacionAsync(turnoDto); }
            catch { /* SignalR es no-crítico */ }
        }

        return turnoDto;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var turno = await _turnoRepository.GetByIdAsync(id);
        if (turno is null) return false;

        _turnoRepository.Delete(turno);
        await _turnoRepository.SaveChangesAsync();
        return true;
    }

    // ─── Comentarios ─────────────────────────────────────────────────────────────

    public async Task<List<ComentarioTurnoResponseDto>> GetComentariosAsync(Guid turnoId)
    {
        var comentarios = await _comentarioRepository.GetByTurnoIdAsync(turnoId);
        return comentarios.Select(MapComentario).ToList();
    }

    public async Task<ComentarioTurnoResponseDto> CrearComentarioAsync(
        Guid turnoId, Guid autorId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("El texto del comentario no puede estar vacío.");

        var turno = await _turnoRepository.GetByIdAsync(turnoId)
            ?? throw new InvalidOperationException("Turno no encontrado.");

        var comentario = new ComentarioTurno
        {
            Id             = Guid.NewGuid(),
            TurnoId        = turnoId,
            AutorUsuarioId = autorId,
            Texto          = texto.Trim(),
            Fecha          = DateTime.UtcNow,
        };

        await _comentarioRepository.AddAsync(comentario);
        await _comentarioRepository.SaveChangesAsync();

        // Recargar con el autor incluido
        var completo = await _comentarioRepository.GetByIdAsync(comentario.Id);
        return MapComentario(completo!);
    }

    public async Task<bool> EliminarComentarioAsync(Guid comentarioId, Guid autorId)
    {
        var comentario = await _comentarioRepository.GetByIdAsync(comentarioId);
        if (comentario is null) return false;
        if (comentario.AutorUsuarioId != autorId)
            throw new InvalidOperationException("Solo podés eliminar tus propios comentarios.");

        _comentarioRepository.Delete(comentario);
        await _comentarioRepository.SaveChangesAsync();
        return true;
    }

    private static ComentarioTurnoResponseDto MapComentario(ComentarioTurno c) => new()
    {
        Id             = c.Id,
        TurnoId        = c.TurnoId,
        AutorUsuarioId = c.AutorUsuarioId,
        AutorNombre    = c.Autor is not null
                         ? $"{c.Autor.Nombre} {c.Autor.Apellido}".Trim()
                         : string.Empty,
        Texto          = c.Texto,
        Fecha          = c.Fecha,
    };

    private static TurnoResponseDto MapToResponse(Turno t) => new()
    {
        Id = t.Id,
        SucursalId = t.SucursalId,
        ClienteId = t.ClienteId,
        ClienteNombre = t.Cliente is not null ? $"{t.Cliente.Nombre} {t.Cliente.Apellido}".Trim() : string.Empty,
        ClienteTelefono = t.Cliente?.Telefono ?? string.Empty,
        ServicioId = t.ServicioId,
        ServicioNombre = t.Servicio?.Nombre ?? string.Empty,
        ProfesionalId = t.ProfesionalId,
        ProfesionalNombre = t.Profesional is not null ? $"{t.Profesional.Nombre} {t.Profesional.Apellido}".Trim() : string.Empty,
        FechaHoraInicio = t.FechaHoraInicio,
        FechaHoraFin = t.FechaHoraFin,
        Estado = t.Estado,
        Origen = t.Origen,
        Observaciones = t.Observaciones,
        TokenGestion = t.TokenGestion,
        PrecioServicio = t.PrecioServicio,
        MontoSena = t.MontoSena,
        TotalCobrado = t.TotalCobrado
    };
}
