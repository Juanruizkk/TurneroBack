using TurneroApp.Application.DTOs.Caja;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class CajaService
{
    private readonly ICajaRepository  _cajaRepository;
    private readonly ITurnoRepository _turnoRepository;

    public CajaService(ICajaRepository cajaRepository, ITurnoRepository turnoRepository)
    {
        _cajaRepository  = cajaRepository;
        _turnoRepository = turnoRepository;
    }

    // ── Consultas ─────────────────────────────────────────────────────────────

    public async Task<CajaResponseDto?> GetBySucursalAsync(Guid sucursalId)
    {
        var caja = await _cajaRepository.GetBySucursalAsync(sucursalId);
        return caja is null ? null : MapToResponse(caja);
    }

    public async Task<IEnumerable<MovimientoCajaResponseDto>> GetMovimientosAsync(
        Guid cajaId, DateTime? desde = null, DateTime? hasta = null)
    {
        var movimientos = await _cajaRepository.GetMovimientosByCajaAsync(cajaId, desde, hasta);
        return movimientos.Select(MapMovimientoToResponse);
    }

    // ── Registro de movimiento ────────────────────────────────────────────────

    /// <summary>
    /// Registra un movimiento de caja con autorización contextual y transacción atómica.
    /// </summary>
    /// <param name="cajaId">Id de la caja donde se registra el movimiento.</param>
    /// <param name="dto">Datos del movimiento.</param>
    /// <param name="usuarioId">Id del usuario que realiza la operación (extraído del JWT).</param>
    /// <param name="rolUsuario">Rol del usuario en texto (extraído del JWT).</param>
    public async Task<MovimientoCajaResponseDto> RegistrarMovimientoAsync(
        Guid                  cajaId,
        RegistrarMovimientoDto dto,
        Guid                  usuarioId,
        string                rolUsuario)
    {
        _ = await _cajaRepository.GetByIdAsync(cajaId)
            ?? throw new InvalidOperationException($"Caja con Id {cajaId} no encontrada.");

        Turno?    turno     = null;
        PagoTurno? pagoTurno = null;

        if (dto.TipoMovimiento == TipoMovimientoCaja.CobroTurno)
        {
            if (!dto.TurnoId.HasValue)
                throw new InvalidOperationException(
                    "TurnoId es obligatorio para movimientos de tipo CobroTurno.");

            turno = await _turnoRepository.GetByIdAsync(dto.TurnoId.Value)
                ?? throw new InvalidOperationException(
                    $"Turno con Id {dto.TurnoId} no encontrado.");

            // ── Autorización contextual ──────────────────────────────────────
            // Un Profesional solo puede registrar cobros de sus propios turnos.
            // Caja y Administrativo pueden cobrar cualquier turno.
            if (string.Equals(rolUsuario, RolUsuario.Profesional.ToString(),
                              StringComparison.OrdinalIgnoreCase)
                && turno.ProfesionalId != usuarioId)
            {
                throw new UnauthorizedAccessException(
                    "Un profesional solo puede registrar cobros de sus propios turnos.");
            }

            // Ajuste descuenta; cualquier otro concepto suma
            if (dto.ConceptoPago == ConceptoPagoTurno.Ajuste)
                turno.TotalCobrado -= dto.Monto;
            else
                turno.TotalCobrado += dto.Monto;
        }

        var movimiento = new MovimientoCaja
        {
            Id             = Guid.NewGuid(),
            CajaId         = cajaId,
            UsuarioId      = usuarioId,
            TurnoId        = dto.TurnoId,
            TipoMovimiento = dto.TipoMovimiento,
            MedioPago      = dto.MedioPago,
            Monto          = dto.Monto,
            Observaciones  = dto.Observaciones,
            Fecha          = DateTime.UtcNow,
        };

        // Crear PagoTurno solo cuando hay turno y se especificó el concepto de pago
        if (turno is not null && dto.ConceptoPago.HasValue)
        {
            // Corregir automáticamente el concepto según si el pago cubre el total o no.
            // Aplica solo a PagoParcial/PagoTotal; Sena y SaldoFinal se respetan tal cual.
            var concepto = dto.ConceptoPago.Value;
            if (concepto is ConceptoPagoTurno.PagoParcial or ConceptoPagoTurno.PagoTotal)
                concepto = turno.TotalCobrado >= turno.PrecioServicio
                    ? ConceptoPagoTurno.PagoTotal
                    : ConceptoPagoTurno.PagoParcial;

            pagoTurno = new PagoTurno
            {
                Id               = Guid.NewGuid(),
                TurnoId          = turno.Id,
                MovimientoCajaId = movimiento.Id,
                Concepto         = concepto,
                Monto            = dto.Monto,
                Fecha            = movimiento.Fecha,
            };
        }

        var resultado = await _cajaRepository.RegistrarMovimientoTransaccionalAsync(
            movimiento, pagoTurno, turno);

        return MapMovimientoToResponse(resultado);
    }

    public async Task<IEnumerable<PagoTurnoResponseDto>> GetPagosByTurnoAsync(Guid turnoId)
    {
        var pagos = await _cajaRepository.GetPagosByTurnoAsync(turnoId);
        return pagos.Select(p => new PagoTurnoResponseDto
        {
            Id       = p.Id,
            Concepto = p.Concepto,
            Monto    = p.Monto,
            Fecha    = p.Fecha,
        });
    }

    // ── Resumen de liquidaciones ──────────────────────────────────────────────

    /// <summary>
    /// Calcula el resumen financiero de un período y las liquidaciones por profesional.
    /// Si se omite <paramref name="hastaParam"/>, el período cubre solo el día de <paramref name="desde"/>.
    /// </summary>
    public async Task<ResumenLiquidacionDto> GetResumenLiquidacionesAsync(
        Guid      cajaId,
        DateTime  desde,
        DateTime? hastaParam = null)
    {
        _ = await _cajaRepository.GetByIdAsync(cajaId)
            ?? throw new InvalidOperationException($"Caja con Id {cajaId} no encontrada.");

        // Rango: desde 00:00:00 del día de inicio hasta 23:59:59.9999999 del día de fin
        var desdeUtc = desde.Date.ToUniversalTime();
        var hastaUtc = (hastaParam ?? desde).Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        var todos  = (await _cajaRepository.GetMovimientosRangoAsync(cajaId, desdeUtc, hastaUtc)).ToList();
        var cobros = (await _cajaRepository.GetCobrosTurnoConProfesionalAsync(cajaId, desdeUtc, hastaUtc)).ToList();

        // Ingresos: CobroTurno, IngresoManual y Ajuste con monto positivo
        var totalIngresos = todos
            .Where(m => m.TipoMovimiento is TipoMovimientoCaja.CobroTurno
                                         or TipoMovimientoCaja.IngresoManual
                     || (m.TipoMovimiento == TipoMovimientoCaja.Ajuste && m.Monto > 0))
            .Sum(m => m.Monto);

        // Egresos: Gasto, Retiro y Ajuste con monto negativo (se devuelve en positivo)
        var totalEgresos = todos
            .Where(m => m.TipoMovimiento is TipoMovimientoCaja.Gasto
                                         or TipoMovimientoCaja.Retiro
                     || (m.TipoMovimiento == TipoMovimientoCaja.Ajuste && m.Monto < 0))
            .Sum(m => Math.Abs(m.Monto));

        // Agrupar cobros de turno por profesional
        var liquidaciones = cobros
            .Where(m => m.Turno is not null)
            .GroupBy(m => m.Turno!.ProfesionalId)
            .Select(g =>
            {
                var prof = g.First().Turno!.Profesional;
                return new LiquidacionProfesionalDto
                {
                    ProfesionalId           = g.Key,
                    Nombre                  = prof is not null
                                                ? $"{prof.Nombre} {prof.Apellido}".Trim()
                                                : g.Key.ToString(),
                    CantidadTurnosAtendidos = g.Select(m => m.TurnoId).Distinct().Count(),
                    TotalGenerado           = g.Sum(m => m.Monto),
                };
            })
            .OrderByDescending(l => l.TotalGenerado)
            .ToList();

        return new ResumenLiquidacionDto
        {
            FechaDesde                  = desdeUtc,
            FechaHasta                  = hastaUtc,
            TotalIngresos               = totalIngresos,
            TotalEgresos                = totalEgresos,
            SaldoPeriodo                = totalIngresos - totalEgresos,
            LiquidacionesPorProfesional = liquidaciones,
        };
    }

    // ── Sesiones de caja ──────────────────────────────────────────────────────

    public async Task<CajaSesionResponseDto?> GetSesionActivaAsync(Guid cajaId)
    {
        var sesion = await _cajaRepository.GetSesionActivaByCajaAsync(cajaId);
        return sesion is null ? null : MapSesionToResponse(sesion);
    }

    public async Task<CajaSesionResponseDto> AbrirSesionAsync(
        Guid          cajaId,
        AbrirSesionDto dto,
        Guid          usuarioId)
    {
        _ = await _cajaRepository.GetByIdAsync(cajaId)
            ?? throw new InvalidOperationException($"Caja con Id {cajaId} no encontrada.");

        var activa = await _cajaRepository.GetSesionActivaByCajaAsync(cajaId);
        if (activa is not null)
            throw new InvalidOperationException("Ya hay una sesión abierta para esta caja.");

        var sesion = new CajaSesion
        {
            Id                   = Guid.NewGuid(),
            CajaId               = cajaId,
            FechaApertura        = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            MontoInicial         = dto.MontoInicial,
            Estado               = Domain.Enums.EstadoCajaSesion.Abierta,
            UsuarioAperturaId    = usuarioId,
            ObservacionesApertura = dto.Observaciones,
        };

        var resultado = await _cajaRepository.AbrirSesionAsync(sesion);

        // Recargar con el nav de UsuarioApertura para el mapper
        var conNav = await _cajaRepository.GetSesionActivaByCajaAsync(cajaId);
        return MapSesionToResponse(conNav ?? resultado);
    }

    public async Task<CajaSesionResponseDto> CerrarSesionAsync(
        Guid          cajaId,
        CerrarSesionDto dto,
        Guid          usuarioId)
    {
        var sesion = await _cajaRepository.GetSesionActivaByCajaAsync(cajaId)
            ?? throw new InvalidOperationException("No hay ninguna sesión activa para esta caja.");

        // Calcular monto esperado con los movimientos de la sesión
        var movimientosSesion = sesion.Movimientos.ToList();

        // Si la nav collection no está cargada, realizar query directa
        if (!movimientosSesion.Any())
        {
            // FechaApertura viene de timestamp without time zone → Kind=Unspecified;
            // hay que decirle a Npgsql que es UTC para comparar con movimientos_caja.Fecha (timestamptz)
            var desdeUtc = DateTime.SpecifyKind(sesion.FechaApertura, DateTimeKind.Utc);
            var hastaUtc = DateTime.UtcNow;
            var todos = (await _cajaRepository.GetMovimientosRangoAsync(
                cajaId, desdeUtc, hastaUtc)).ToList();
            movimientosSesion = todos
                .Where(m => m.CajaSesionId == sesion.Id)
                .ToList();
        }

        var ingresos = movimientosSesion
            .Where(m => m.TipoMovimiento is TipoMovimientoCaja.CobroTurno
                                         or TipoMovimientoCaja.IngresoManual
                     || (m.TipoMovimiento == TipoMovimientoCaja.Ajuste && m.Monto > 0))
            .Sum(m => m.Monto);

        var egresos = movimientosSesion
            .Where(m => m.TipoMovimiento is TipoMovimientoCaja.Gasto
                                         or TipoMovimientoCaja.Retiro
                     || (m.TipoMovimiento == TipoMovimientoCaja.Ajuste && m.Monto < 0))
            .Sum(m => Math.Abs(m.Monto));

        var montoFinalEsperado = sesion.MontoInicial + ingresos - egresos;

        var resultado = await _cajaRepository.CerrarSesionAsync(
            sesion.Id,
            dto.MontoFinalReal,
            montoFinalEsperado,
            usuarioId,
            dto.Observaciones);

        return MapSesionToResponse(resultado);
    }

    public async Task<IEnumerable<CajaSesionResponseDto>> GetHistorialSesionesAsync(Guid cajaId)
    {
        var sesiones = await _cajaRepository.GetSesionesByCajaAsync(cajaId);
        return sesiones.Select(MapSesionToResponse);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static CajaResponseDto MapToResponse(Caja c) => new()
    {
        Id         = c.Id,
        SucursalId = c.SucursalId,
        Nombre     = c.Nombre,
        Activa     = c.Activa,
    };

    private static MovimientoCajaResponseDto MapMovimientoToResponse(MovimientoCaja m) => new()
    {
        Id             = m.Id,
        CajaId         = m.CajaId,
        UsuarioId      = m.UsuarioId,
        TurnoId        = m.TurnoId,
        TipoMovimiento = m.TipoMovimiento,
        MedioPago      = m.MedioPago,
        Monto          = m.Monto,
        Observaciones  = m.Observaciones,
        Fecha          = m.Fecha,
        ConceptoPago   = m.PagoTurno?.Concepto,
        Turno          = m.Turno is null ? null : new TurnoResumenCajaDto
        {
            Id                = m.Turno.Id,
            ClienteNombre     = $"{m.Turno.Cliente?.Nombre} {m.Turno.Cliente?.Apellido}".Trim(),
            ServicioNombre    = m.Turno.Servicio?.Nombre ?? string.Empty,
            ProfesionalNombre = $"{m.Turno.Profesional?.Nombre} {m.Turno.Profesional?.Apellido}".Trim(),
            FechaHoraInicio   = m.Turno.FechaHoraInicio,
            PrecioServicio    = m.Turno.PrecioServicio,
            TotalCobrado      = m.Turno.TotalCobrado,
        },
    };

    private static CajaSesionResponseDto MapSesionToResponse(CajaSesion s) => new()
    {
        Id                    = s.Id,
        CajaId                = s.CajaId,
        FechaApertura         = s.FechaApertura,
        FechaCierre           = s.FechaCierre,
        MontoInicial          = s.MontoInicial,
        MontoFinalEsperado    = s.MontoFinalEsperado,
        MontoFinalReal        = s.MontoFinalReal,
        Diferencia            = s.Diferencia,
        Estado                = s.Estado.ToString(),
        UsuarioAperturaNombre = s.UsuarioApertura is not null
                                  ? $"{s.UsuarioApertura.Nombre} {s.UsuarioApertura.Apellido}".Trim()
                                  : null,
        UsuarioCierreNombre   = s.UsuarioCierre is not null
                                  ? $"{s.UsuarioCierre.Nombre} {s.UsuarioCierre.Apellido}".Trim()
                                  : null,
        ObservacionesApertura = s.ObservacionesApertura,
        ObservacionesCierre   = s.ObservacionesCierre,
    };
}
