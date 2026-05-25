using TurneroApp.Application.DTOs.Auth;
using TurneroApp.Application.Interfaces;
using TurneroApp.Application.Utilities;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Application.Services;

public class RegisterService
{
    private readonly IUsuarioRepository           _usuarioRepo;
    private readonly IUsuarioSucursalRolRepository _usrRepo;
    private readonly ISucursalRepository          _sucursalRepo;
    private readonly ICajaRepository              _cajaRepo;
    private readonly ISuscripcionRepository       _suscripcionRepo;
    private readonly IEmailService                _emailService;
    private readonly string                       _frontendUrl;

    private static readonly PermisoSistema[] TodosLosPermisos =
    [
        PermisoSistema.AgendarTurnos,
        PermisoSistema.EditarTurnos,
        PermisoSistema.CargarCobranzas,
        PermisoSistema.VerTelefonoClientes,
    ];

    public RegisterService(
        IUsuarioRepository            usuarioRepo,
        IUsuarioSucursalRolRepository usrRepo,
        ISucursalRepository           sucursalRepo,
        ICajaRepository               cajaRepo,
        ISuscripcionRepository        suscripcionRepo,
        IEmailService                 emailService,
        string                        frontendUrl)
    {
        _usuarioRepo     = usuarioRepo;
        _usrRepo         = usrRepo;
        _sucursalRepo    = sucursalRepo;
        _cajaRepo        = cajaRepo;
        _suscripcionRepo = suscripcionRepo;
        _emailService    = emailService;
        _frontendUrl     = frontendUrl;
    }

    /// <summary>
    /// Crea una sucursal nueva + usuario owner + caja + suscripción trial.
    /// Lanza <see cref="InvalidOperationException"/> si el email ya está registrado.
    /// </summary>
    public async Task<UsuarioAutenticadoDto> RegistrarAsync(RegisterDto dto)
    {
        var emailNorm = dto.Email.ToLowerInvariant().Trim();

        // 1. Verificar que el email no exista ya
        var existing = await _usuarioRepo.GetByEmailAsync(emailNorm);
        if (existing is not null)
            throw new InvalidOperationException("El email ya está registrado. Podés iniciar sesión o recuperar tu contraseña.");

        // 2. Crear Sucursal
        var nombreNegocio = dto.NombreNegocio.Trim();
        var slug = await SlugHelper.GenerateUniqueSlugAsync(
            nombreNegocio,
            async c => await _sucursalRepo.GetBySlugAsync(c) is not null);

        var sucursal = new Sucursal
        {
            Id            = Guid.NewGuid(),
            Nombre        = nombreNegocio,
            Slug          = slug,
            Direccion     = string.Empty,
            Telefono      = string.Empty,
            Email         = emailNorm,
            Activa        = true,
            FechaCreacion = DateTime.UtcNow,
        };
        await _sucursalRepo.AddAsync(sucursal);

        // 3. Crear Usuario
        var usuario = new Usuario
        {
            Id            = Guid.NewGuid(),
            Nombre        = dto.Nombre.Trim(),
            Apellido      = dto.Apellido.Trim(),
            Email         = emailNorm,
            Telefono      = string.Empty,
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FechaCreacion = DateTime.UtcNow,
        };
        await _usuarioRepo.AddAsync(usuario);

        // 4. Membresía owner con todos los permisos
        var ownerUsr = new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = usuario.Id,
            SucursalId    = sucursal.Id,
            Rol           = RolUsuario.Administrativo,
            EsOwner       = true,
            Estado        = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow,
            Permisos      = TodosLosPermisos.Select(p => new UsuarioPermiso
            {
                Id                  = Guid.NewGuid(),
                UsuarioSucursalRolId = Guid.Empty, // se asigna abajo
                Permiso              = p,
            }).ToList(),
        };

        foreach (var perm in ownerUsr.Permisos)
            perm.UsuarioSucursalRolId = ownerUsr.Id;

        await _usrRepo.AddAsync(ownerUsr);

        // 5. Caja de la sucursal
        await _cajaRepo.AddAsync(new Caja
        {
            Id         = Guid.NewGuid(),
            SucursalId = sucursal.Id,
            Nombre     = $"Caja — {dto.NombreNegocio.Trim()}",
            Activa     = true,
        });

        // 6. Suscripción de prueba (14 días)
        var planBasico = await _suscripcionRepo.GetPlanBasicoAsync();
        if (planBasico is not null)
        {
            var ahora = DateTime.UtcNow;
            await _suscripcionRepo.AddAsync(new SucursalSuscripcion
            {
                Id               = Guid.NewGuid(),
                SucursalId       = sucursal.Id,
                PlanId           = planBasico.Id,
                Estado           = EstadoSuscripcion.PruebActiva,
                FechaInicio      = ahora,
                FechaVencimiento = ahora.AddDays(planBasico.DuracionTrialDias),
                EnPeriodoPrueba  = true,
            });
        }

        // 7. Guardar todo
        await _sucursalRepo.SaveChangesAsync();

        // 8. Email de bienvenida (fire-and-forget, no bloquea el registro)
        _ = EnviarEmailBienvenidaAsync(usuario.Nombre, emailNorm, nombreNegocio, _frontendUrl);

        return new UsuarioAutenticadoDto
        {
            Rol          = ownerUsr.Rol.ToString(),
            Nombre       = usuario.Nombre,
            Apellido     = usuario.Apellido,
            Email        = usuario.Email,
            EsSuperAdmin = false,
            EsOwner      = true,
            UserId       = usuario.Id,
            SucursalId   = sucursal.Id,
            Permisos     = TodosLosPermisos.Select(p => p.ToString()).ToList(),
        };
    }

    private async Task EnviarEmailBienvenidaAsync(string nombre, string email, string nombreNegocio, string frontendUrl)
    {
        try
        {
            await _emailService.EnviarBienvenidaAsync(email, nombre, nombreNegocio, frontendUrl);
        }
        catch (Exception ex)
        {
            // No propagamos errores de email — el registro ya fue exitoso
            Console.Error.WriteLine($"[RegisterService] Error al enviar email de bienvenida a {email} ({nombreNegocio}): {ex.Message}");
        }
    }
}
