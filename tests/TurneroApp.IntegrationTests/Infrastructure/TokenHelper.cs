using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TurneroApp.IntegrationTests.Infrastructure;

public static class TokenHelper
{
    /// <summary>
    /// Genera un JWT firmado con el secret de test, válido para el middleware
    /// de autenticación de la aplicación levantada por <see cref="HttpTestFixture"/>.
    /// </summary>
    /// <param name="sucursalId">SucursalId del claim. Null si no aplica (ej: SuperAdmin).</param>
    /// <param name="rol">Rol del usuario. Default: "Administrativo".</param>
    /// <param name="esSuperAdmin">Si true, el claim esSuperAdmin = "true".</param>
    public static string GenerarToken(
        Guid?  sucursalId  = null,
        string rol         = "Administrativo",
        bool   esSuperAdmin = false)
    {
        var claims = new List<Claim>
        {
            new("userId",       Guid.NewGuid().ToString()),
            new("esSuperAdmin", esSuperAdmin.ToString().ToLower()),
            new(ClaimTypes.Role, rol),
        };

        if (sucursalId.HasValue)
            claims.Add(new Claim("sucursalId", sucursalId.Value.ToString()));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             "TurneroApp",
            audience:           "TurneroApp",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Token de SuperAdmin (sin sucursalId).</summary>
    public static string SuperAdmin() =>
        GenerarToken(esSuperAdmin: true, rol: "SuperAdmin");

    /// <summary>Token de usuario normal vinculado a una sucursal específica.</summary>
    public static string ParaSucursal(Guid sucursalId, string rol = "Administrativo") =>
        GenerarToken(sucursalId: sucursalId, rol: rol);
}
