using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Application.Interfaces;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ISucursalRepository _sucursalRepository;
    private readonly IEmailService _emailService;
    private readonly string _frontendUrl;
    private readonly IHorarioLaboralRepository _horarioRepo;
    private readonly IUsuarioServicioRepository _usuarioServicioRepo;

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
        ISucursalRepository sucursalRepository,
        IEmailService emailService,
        string frontendUrl,
        IHorarioLaboralRepository horarioRepo,
        IUsuarioServicioRepository usuarioServicioRepo)
    {
        _usuarioRepository   = usuarioRepository;
        _sucursalRepository  = sucursalRepository;
        _emailService        = emailService;
        _frontendUrl         = frontendUrl;
        _horarioRepo         = horarioRepo;
        _usuarioServicioRepo = usuarioServicioRepo;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<UsuarioResponseDto>> GetBySucursalAsync(Guid sucursalId)
    {
        var usuarios = await _usuarioRepository.GetBySucursalAsync(sucursalId);
        return usuarios.Select(MapToResponse);
    }

    public async Task<UsuarioResponseDto> GetByIdAsync(Guid id)
    {
        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return MapToResponse(usuario);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    public async Task<UsuarioResponseDto> CreateAsync(CreateUsuarioDto dto)
    {
        ValidarPermisos(dto.Permisos);

        var existente = await _usuarioRepository.GetByEmailAsync(dto.SucursalId, dto.Email);
        if (existente is not null)
            throw new InvalidOperationException(
                $"Ya existe un usuario con el email '{dto.Email}' en esta sucursal.");

        var sucursal = await _sucursalRepository.GetByIdAsync(dto.SucursalId)
            ?? throw new InvalidOperationException($"No se encontró la sucursal con id {dto.SucursalId}.");

        var token = Guid.NewGuid().ToString("N"); // sin guiones, más limpio en URL

        var usuario = new Usuario
        {
            Id                        = Guid.NewGuid(),
            SucursalId                = dto.SucursalId,
            Nombre                    = dto.Nombre.Trim(),
            Apellido                  = dto.Apellido.Trim(),
            Email                     = dto.Email.Trim().ToLowerInvariant(),
            Telefono                  = dto.Telefono.Trim(),
            PasswordHash              = string.Empty,       // se establece al aceptar la invitación
            Rol                       = dto.Rol,
            Estado                    = EstadoUsuario.PendienteInvitacion,
            InvitacionToken           = token,
            FechaExpiracionInvitacion = DateTime.UtcNow.AddDays(7),
        };

        foreach (var permiso in dto.Permisos.Distinct())
        {
            usuario.Permisos.Add(new UsuarioPermiso
            {
                Id        = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                Permiso   = permiso
            });
        }

        await _usuarioRepository.AddAsync(usuario);
        await _usuarioRepository.SaveChangesAsync();

        // Enviar email (no bloquea el guardado si falla)
        try
        {
            var link = $"{_frontendUrl}/invitacion/{token}";
            await _emailService.EnviarInvitacionAsync(
                usuario.Email,
                $"{usuario.Nombre} {usuario.Apellido}",
                sucursal.Nombre,
                link);
        }
        catch
        {
            // Log en producción. El usuario quedó creado; la invitación se puede reenviar.
        }

        return MapToResponse(usuario);
    }

    public async Task<UsuarioResponseDto> UpdateAsync(Guid id, UpdateUsuarioDto dto)
    {
        ValidarPermisos(dto.Permisos);

        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        var emailNormalizado = dto.Email.Trim().ToLowerInvariant();
        if (!string.Equals(usuario.Email, emailNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            var conMismoEmail = await _usuarioRepository.GetByEmailAsync(usuario.SucursalId, emailNormalizado);
            if (conMismoEmail is not null)
                throw new InvalidOperationException(
                    $"Ya existe otro usuario con el email '{dto.Email}' en esta sucursal.");
        }

        usuario.Nombre   = dto.Nombre.Trim();
        usuario.Apellido = dto.Apellido.Trim();
        usuario.Email    = emailNormalizado;
        usuario.Telefono = dto.Telefono.Trim();
        usuario.Rol      = dto.Rol;
        usuario.Estado   = dto.Estado;

        foreach (var p in usuario.Permisos.ToList())
            usuario.Permisos.Remove(p);

        foreach (var permiso in dto.Permisos.Distinct())
        {
            usuario.Permisos.Add(new UsuarioPermiso
            {
                Id        = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                Permiso   = permiso
            });
        }

        _usuarioRepository.Update(usuario);
        await _usuarioRepository.SaveChangesAsync();

        return MapToResponse(usuario);
    }

    /// <summary>Reenvía el email de invitación regenerando el token.</summary>
    public async Task ReenviarInvitacionAsync(Guid id)
    {
        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        if (usuario.Estado != EstadoUsuario.PendienteInvitacion)
            throw new InvalidOperationException("Solo se puede reenviar la invitación a usuarios pendientes.");

        var sucursal = await _sucursalRepository.GetByIdAsync(usuario.SucursalId)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        // Regenerar token y extender vencimiento
        usuario.InvitacionToken           = Guid.NewGuid().ToString("N");
        usuario.FechaExpiracionInvitacion = DateTime.UtcNow.AddDays(7);

        _usuarioRepository.Update(usuario);
        await _usuarioRepository.SaveChangesAsync();

        var link = $"{_frontendUrl}/invitacion/{usuario.InvitacionToken}";
        await _emailService.EnviarReenvioInvitacionAsync(
            usuario.Email,
            $"{usuario.Nombre} {usuario.Apellido}",
            sucursal.Nombre,
            link);
    }

    /// <summary>Soft delete: marca el usuario como Inactivo.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        usuario.Estado = EstadoUsuario.Inactivo;

        _usuarioRepository.Update(usuario);
        await _usuarioRepository.SaveChangesAsync();
    }

    // ─── Horarios y Servicios ─────────────────────────────────────────────────

    public async Task<List<HorarioLaboralDto>> GetHorariosAsync(Guid id)
    {
        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return usuario.HorariosLaborales.Select(MapHorario).ToList();
    }

    public async Task ReplaceHorariosAsync(Guid id, List<HorarioLaboralDto> dtos)
    {
        _ = await _usuarioRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        var entidades = dtos.Select(d => new HorarioLaboral
        {
            Id        = Guid.NewGuid(),
            UsuarioId = id,
            DiaSemana = (DayOfWeek)d.DiaSemana,
            HoraDesde = TimeSpan.Parse(d.HoraDesde),
            HoraHasta = TimeSpan.Parse(d.HoraHasta),
            Activo    = d.Activo,
        });

        await _horarioRepo.ReplaceAsync(id, entidades);
        await _horarioRepo.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetServiciosAsync(Guid id)
    {
        var usuario = await _usuarioRepository.GetByIdConPermisosAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");
        return usuario.Servicios.Select(s => s.ServicioId).ToList();
    }

    public async Task ReplaceServiciosAsync(Guid id, List<Guid> servicioIds)
    {
        _ = await _usuarioRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"No se encontró el usuario con id {id}.");

        await _usuarioServicioRepo.ReplaceAsync(id, servicioIds);
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

    private static UsuarioResponseDto MapToResponse(Usuario u) => new()
    {
        Id               = u.Id,
        SucursalId       = u.SucursalId,
        Nombre           = u.Nombre,
        Apellido         = u.Apellido,
        Email            = u.Email,
        Telefono         = u.Telefono,
        Rol              = u.Rol,
        Estado           = u.Estado,
        Permisos         = u.Permisos.Select(p => p.Permiso).ToList(),
        HorariosLaborales = u.HorariosLaborales.Select(MapHorario).ToList(),
        ServiciosIds     = u.Servicios.Select(s => s.ServicioId).ToList(),
    };

    private static HorarioLaboralDto MapHorario(HorarioLaboral h) =>
        new((int)h.DiaSemana, h.HoraDesde.ToString(@"hh\:mm"), h.HoraHasta.ToString(@"hh\:mm"), h.Activo);
}
