using Microsoft.Extensions.Logging;
using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUsuarioSucursalRolRepository _usrRepository;
    private readonly ISucursalRepository _sucursalRepository;
    private readonly IEmailService _emailService;
    private readonly string _frontendUrl;
    private readonly IHorarioLaboralRepository _horarioRepo;
    private readonly IUsuarioServicioRepository _usuarioServicioRepo;
    private readonly ITurnoRepository _turnoRepo;
    private readonly ISuscripcionRepository _suscripcionRepo;
    private readonly SuscripcionGuard _suscripcionGuard;
    private readonly ILogger<UsuarioService> _logger;

    private static readonly HashSet<PermisoSistema> PermisosValidos = new()
    {
        PermisoSistema.AgendarTurnos,
        PermisoSistema.EditarTurnos,
        PermisoSistema.CargarCobranzas,
        PermisoSistema.VerTelefonoClientes,
        PermisoSistema.GestionarAjustes
    };

    public UsuarioService(
        IUsuarioRepository usuarioRepository,
        IUsuarioSucursalRolRepository usrRepository,
        ISucursalRepository sucursalRepository,
        IEmailService emailService,
        string frontendUrl,
        IHorarioLaboralRepository horarioRepo,
        IUsuarioServicioRepository usuarioServicioRepo,
        ITurnoRepository turnoRepo,
        ISuscripcionRepository suscripcionRepo,
        SuscripcionGuard suscripcionGuard,
        ILogger<UsuarioService> logger)
    {
        _usuarioRepository   = usuarioRepository;
        _usrRepository       = usrRepository;
        _sucursalRepository  = sucursalRepository;
        _emailService        = emailService;
        _frontendUrl         = frontendUrl;
        _horarioRepo         = horarioRepo;
        _usuarioServicioRepo = usuarioServicioRepo;
        _turnoRepo           = turnoRepo;
        _suscripcionRepo     = suscripcionRepo;
        _suscripcionGuard    = suscripcionGuard;
        _logger              = logger;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<UsuarioResponseDto>> GetBySucursalAsync(Guid sucursalId)
    {
        var roles = await _usrRepository.GetBySucursalAsync(sucursalId);
        return roles.Select(MapToResponse);
    }

    public async Task<UsuarioResponseDto> GetByIdAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return MapToResponse(usr);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    public async Task<UsuarioResponseDto> CreateAsync(CreateUsuarioDto dto)
    {
        await _suscripcionGuard.VerificarActivaAsync(dto.SucursalId);

        ValidarPermisos(dto.Permisos);

        // Validar límite de usuarios del plan
        var suscripcion = await _suscripcionRepo.GetBySucursalAsync(dto.SucursalId);
        if (suscripcion is not null)
        {
            var activos = await _usrRepository.ContarActivosBySucursalAsync(dto.SucursalId);
            if (activos >= suscripcion.Plan.MaxUsuarios)
                throw new InvalidOperationException(
                    $"Tu plan {suscripcion.Plan.Nombre} permite hasta {suscripcion.Plan.MaxUsuarios} " +
                    "usuarios activos. Actualizá tu suscripción para agregar más.");
        }

        var sucursal = await _sucursalRepository.GetByIdAsync(dto.SucursalId)
            ?? throw new InvalidOperationException($"No se encontró la sucursal con id {dto.SucursalId}.");

        var emailNorm = dto.Email.Trim().ToLowerInvariant();
        var token     = Guid.NewGuid().ToString("N");

        // ¿Ya existe un Usuario global con este email?
        var usuarioExistente = await _usuarioRepository.GetByEmailAsync(emailNorm);

        bool esUsuarioNuevo = usuarioExistente is null;

        Usuario usuario;
        if (esUsuarioNuevo)
        {
            // Crear nuevo Usuario (identidad global)
            usuario = new Usuario
            {
                Id           = Guid.NewGuid(),
                Nombre       = dto.Nombre.Trim(),
                Apellido     = dto.Apellido.Trim(),
                Email        = emailNorm,
                Telefono     = dto.Telefono.Trim(),
                PasswordHash = string.Empty,
                FechaCreacion = DateTime.UtcNow,
            };
            await _usuarioRepository.AddAsync(usuario);
        }
        else
        {
            usuario = usuarioExistente!;

            // Verificar que no esté ya en esta sucursal
            var rolExistente = await _usrRepository.GetByUsuarioYSucursalAsync(usuario.Id, dto.SucursalId);
            if (rolExistente is not null)
                throw new InvalidOperationException(
                    $"El usuario con el email '{dto.Email}' ya es miembro de esta sucursal.");
        }

        // Crear la membresía UsuarioSucursalRol
        var usr = new UsuarioSucursalRol
        {
            Id                        = Guid.NewGuid(),
            UsuarioId                 = usuario.Id,
            SucursalId                = dto.SucursalId,
            Rol                       = dto.Rol,
            EsOwner                   = false,
            Estado                    = EstadoUsuario.PendienteInvitacion,
            InvitacionToken           = token,
            FechaExpiracionInvitacion = DateTime.UtcNow.AddDays(7),
            FechaCreacion             = DateTime.UtcNow,
        };

        foreach (var permiso in dto.Permisos.Distinct())
        {
            usr.Permisos.Add(new UsuarioPermiso
            {
                Id                   = Guid.NewGuid(),
                UsuarioSucursalRolId = usr.Id,
                Permiso              = permiso,
            });
        }

        await _usrRepository.AddAsync(usr);
        await _usrRepository.SaveChangesAsync();

        var invitacionLink = $"{_frontendUrl}/invitacion/{token}";

        try
        {
            if (esUsuarioNuevo)
            {
                await _emailService.EnviarInvitacionAsync(
                    usuario.Email,
                    $"{usuario.Nombre} {usuario.Apellido}",
                    sucursal.Nombre,
                    invitacionLink);
            }
            else
            {
                await _emailService.EnviarInvitacionUsuarioExistenteAsync(
                    usuario.Email,
                    $"{usuario.Nombre} {usuario.Apellido}",
                    sucursal.Nombre,
                    invitacionLink);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar email de invitación a {Email}", usuario.Email);
        }

        var response = MapToResponse(usr);
        response.InvitacionLink = invitacionLink;
        return response;
    }

    public async Task<UsuarioResponseDto> UpdateAsync(Guid id, UpdateUsuarioDto dto)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        var emailNormalizado = dto.Email.Trim().ToLowerInvariant();

        // Verificar unicidad global del email si cambió
        if (!string.Equals(usr.Usuario.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            var conMismoEmail = await _usuarioRepository.GetByEmailAsync(emailNormalizado);
            if (conMismoEmail is not null && conMismoEmail.Id != usr.UsuarioId)
                throw new InvalidOperationException(
                    $"Ya existe otro usuario con el email '{dto.Email}'.");
        }

        // Actualizar datos personales en Usuario
        usr.Usuario.Nombre   = dto.Nombre.Trim();
        usr.Usuario.Apellido = dto.Apellido.Trim();
        usr.Usuario.Email    = emailNormalizado;
        usr.Usuario.Telefono = dto.Telefono.Trim();

        _usrRepository.Update(usr);
        await _usrRepository.SaveChangesAsync();

        return MapToResponse(usr);
    }

    /// <summary>
    /// Actualiza el rol, estado y permisos de la membresía de un usuario.
    /// Operación exclusiva para usuarios con permisos administrativos.
    /// </summary>
    public async Task<UsuarioResponseDto> UpdateRolEstadoPermisosAsync(Guid id, UpdateRolEstadoPermisosDto dto)
    {
        ValidarPermisos(dto.Permisos);

        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        usr.Rol    = dto.Rol;
        usr.Estado = dto.Estado;

        foreach (var p in usr.Permisos.ToList())
            usr.Permisos.Remove(p);

        foreach (var permiso in dto.Permisos.Distinct())
        {
            usr.Permisos.Add(new UsuarioPermiso
            {
                Id                   = Guid.NewGuid(),
                UsuarioSucursalRolId = usr.Id,
                Permiso              = permiso,
            });
        }

        _usrRepository.Update(usr);
        await _usrRepository.SaveChangesAsync();

        return MapToResponse(usr);
    }

    /// <summary>Reenvía el email de invitación regenerando el token. Devuelve el link de invitación.</summary>
    public async Task<string> ReenviarInvitacionAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        if (usr.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Solo se puede reenviar la invitación a usuarios pendientes.");

        var sucursal = await _sucursalRepository.GetByIdAsync(usr.SucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        usr.InvitacionToken           = Guid.NewGuid().ToString("N");
        usr.FechaExpiracionInvitacion = DateTime.UtcNow.AddDays(7);

        _usrRepository.Update(usr);
        await _usrRepository.SaveChangesAsync();

        var link = $"{_frontendUrl}/invitacion/{usr.InvitacionToken}";

        try
        {
            await _emailService.EnviarReenvioInvitacionAsync(
                usr.Usuario.Email,
                $"{usr.Usuario.Nombre} {usr.Usuario.Apellido}",
                sucursal.Nombre,
                link);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reenviar email de invitación a {Email}", usr.Usuario.Email);
        }

        return link;
    }

    /// <summary>Soft delete: marca la membresía como Inactiva.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        if (usr.EsOwner)
            throw new InvalidOperationException("No se puede desactivar al dueño del negocio.");

        var tieneTurnosFuturos = await _turnoRepo.TieneTurnosFuturosActivosAsync(usr.UsuarioId);
        if (tieneTurnosFuturos)
            throw new InvalidOperationException(
                "El profesional tiene turnos futuros asignados. Reasignálos o cancelálos antes de desactivarlo.");

        usr.Estado = EstadoUsuario.Inactivo;

        _usrRepository.Update(usr);
        await _usrRepository.SaveChangesAsync();
    }

    /// <summary>Hard delete: elimina físicamente la membresía y sus hijos.</summary>
    public async Task HardDeleteAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (usr.EsOwner)
            throw new InvalidOperationException("No se puede eliminar al dueño del negocio.");

        var tieneTurnosFuturos = await _turnoRepo.TieneTurnosFuturosActivosAsync(usr.UsuarioId);
        if (tieneTurnosFuturos)
            throw new InvalidOperationException(
                "El profesional tiene turnos futuros asignados. Reasignálos o cancelálos antes de eliminarlo.");

        await _usrRepository.DeleteHardAsync(id);
    }

    // ─── Horarios y Servicios ─────────────────────────────────────────────────

    public async Task<List<HorarioLaboralDto>> GetHorariosAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return usr.HorariosLaborales.Select(MapHorario).ToList();
    }

    public async Task ReplaceHorariosAsync(Guid id, List<HorarioLaboralDto> dtos)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        var entidades = dtos.Select(d =>
        {
            if (!TimeSpan.TryParse(d.HoraDesde, out var horaDesde))
                throw new InvalidOperationException($"Formato de hora inválido: '{d.HoraDesde}'. Use HH:mm.");
            if (!TimeSpan.TryParse(d.HoraHasta, out var horaHasta))
                throw new InvalidOperationException($"Formato de hora inválido: '{d.HoraHasta}'. Use HH:mm.");

            return new HorarioLaboral
            {
                Id                   = Guid.NewGuid(),
                UsuarioSucursalRolId = usr.Id,
                DiaSemana            = (DayOfWeek)d.DiaSemana,
                HoraDesde            = horaDesde,
                HoraHasta            = horaHasta,
                Activo               = d.Activo,
            };
        });

        await _horarioRepo.ReplaceAsync(usr.Id, entidades);
        await _horarioRepo.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetServiciosAsync(Guid id)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return usr.Servicios.Select(s => s.ServicioId).ToList();
    }

    public async Task ReplaceServiciosAsync(Guid id, List<Guid> servicioIds)
    {
        var usr = await _usrRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        await _usuarioServicioRepo.ReplaceAsync(usr.Id, servicioIds);
        await _usuarioServicioRepo.SaveChangesAsync();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void ValidarPermisos(IEnumerable<PermisoSistema> permisos)
    {
        var invalidos = permisos.Where(p => !PermisosValidos.Contains(p)).ToList();
        if (invalidos.Any())
            throw new InvalidOperationException(
                $"Los siguientes permisos no son válidos: {string.Join(", ", invalidos)}.");
    }

    private static UsuarioResponseDto MapToResponse(UsuarioSucursalRol usr) => new()
    {
        Id               = usr.Id,
        UsuarioId        = usr.UsuarioId,
        SucursalId       = usr.SucursalId,
        Nombre           = usr.Usuario.Nombre,
        Apellido         = usr.Usuario.Apellido,
        Email            = usr.Usuario.Email,
        Telefono         = usr.Usuario.Telefono,
        Rol              = usr.Rol,
        Estado           = usr.Estado,
        EsOwner          = usr.EsOwner,
        Permisos         = usr.Permisos.Select(p => p.Permiso).ToList(),
        HorariosLaborales = usr.HorariosLaborales.Select(MapHorario).ToList(),
        ServiciosIds     = usr.Servicios.Select(s => s.ServicioId).ToList(),
    };

    private static HorarioLaboralDto MapHorario(HorarioLaboral h) =>
        new((int)h.DiaSemana, h.HoraDesde.ToString(@"hh\:mm"), h.HoraHasta.ToString(@"hh\:mm"), h.Activo);
}
