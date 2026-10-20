using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TurneroApp.Application.DTOs.Auth;

namespace TurneroApp.Api.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public LoginResponseDto GenerarRespuesta(UsuarioAutenticadoDto usuario)
    {
        var claims = BuildClaims(usuario);
        var token  = GenerarToken(claims);

        return new LoginResponseDto
        {
            Token        = token,
            Rol          = usuario.Rol,
            Nombre       = usuario.Nombre,
            Apellido     = usuario.Apellido,
            Email        = usuario.Email,
            EsSuperAdmin = usuario.EsSuperAdmin,
            EsOwner      = usuario.EsOwner,
            UserId       = usuario.UserId,
            SucursalId   = usuario.SucursalId,
            Permisos     = usuario.Permisos,
        };
    }

    private static List<Claim> BuildClaims(UsuarioAutenticadoDto u)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   u.UserId?.ToString() ?? "superadmin"),
            new(JwtRegisteredClaimNames.Email, u.Email),
            new(ClaimTypes.Role,               u.Rol),
            new("esSuperAdmin",                u.EsSuperAdmin.ToString().ToLower()),
            new("esOwner",                     u.EsOwner.ToString().ToLower()),
            new("nombre",                      u.Nombre),
            new("apellido",                    u.Apellido),
        };

        if (u.SucursalId.HasValue)
            claims.Add(new("sucursalId", u.SucursalId.Value.ToString()));

        if (u.UserId.HasValue)
            claims.Add(new("userId", u.UserId.Value.ToString()));

        foreach (var p in u.Permisos)
            claims.Add(new("permiso", p));

        return claims;
    }

    private string GenerarToken(IEnumerable<Claim> claims)
    {
        var secret   = _config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret no configurado.");
        var issuer   = _config["Jwt:Issuer"]   ?? "TurneroApp";
        var audience = _config["Jwt:Audience"] ?? "TurneroAppClients";
        var hours    = int.Parse(_config["Jwt:ExpirationHours"] ?? "24");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)); // NOSONAR - secret comes from env vars at runtime; appsettings.json only holds a placeholder
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
