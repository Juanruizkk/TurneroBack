using TurneroApp.Application.DTOs.Usuario;
using TurneroApp.Domain.Entities;
using TurneroApp.Domain.Enums;

namespace TurneroApp.Tests.Helpers;

internal static class UsuarioTestFactory
{
    public static Usuario CrearUsuario(string email = "test@test.com", string password = "pass123")
    {
        return new Usuario
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Nombre Test",
            Apellido      = "Apellido Test",
            Email         = email,
            Telefono      = "1111111111",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(password),
            FechaCreacion = DateTime.UtcNow,
        };
    }

    public static UsuarioSucursalRol CrearUSR(
        Usuario? usuario = null,
        EstadoUsuario estado = EstadoUsuario.Activo,
        Guid? sucursalId = null,
        RolUsuario rol = RolUsuario.Profesional)
    {
        var u   = usuario ?? CrearUsuario();
        var sid = sucursalId ?? Guid.NewGuid();

        return new UsuarioSucursalRol
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = u.Id,
            SucursalId    = sid,
            Rol           = rol,
            EsOwner       = false,
            Estado        = estado,
            FechaCreacion = DateTime.UtcNow,
            Usuario       = u,
            Sucursal      = new Sucursal { Id = sid, Nombre = "Sucursal Test" },
            Permisos      = [],
        };
    }

    public static Sucursal CrearSucursal(string nombre = "Sucursal Test")
    {
        return new Sucursal
        {
            Id     = Guid.NewGuid(),
            Nombre = nombre,
        };
    }

    public static UsuarioSucursalRol CrearUSROwner(Usuario? usuario = null, Guid? sucursalId = null)
    {
        var usr = CrearUSR(usuario, sucursalId: sucursalId);
        usr.EsOwner = true;
        return usr;
    }

    public static UsuarioSucursalRol CrearUSRPendiente(Usuario? usuario = null, Guid? sucursalId = null)
    {
        return CrearUSR(usuario, estado: EstadoUsuario.PendienteInvitacion, sucursalId: sucursalId);
    }

    public static CreateUsuarioDto CrearCreateDto(Guid? sucursalId = null, string email = "nuevo@test.com")
    {
        return new CreateUsuarioDto
        {
            SucursalId = sucursalId ?? Guid.NewGuid(),
            Nombre     = "Nuevo",
            Apellido   = "Usuario",
            Email      = email,
            Telefono   = "2222222222",
            Rol        = RolUsuario.Profesional,
            Permisos   = [],
        };
    }
}
