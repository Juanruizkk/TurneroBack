using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TurneroApp.Application.Utilities;

public static class SlugHelper
{
    private static readonly Regex _nonAlphanumeric  = new(@"[^a-z0-9]+",   RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex _trailingHyphens  = new(@"^-+|-+$",       RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex _validSlugPattern = new(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Palabras reservadas que no pueden usarse como slug de sucursal.
    /// </summary>
    public static readonly HashSet<string> PalabrasProhibidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "login", "register", "dashboard", "app",
        "planes", "pricing", "soporte", "www", "superadmin", "publico",
        "auth", "reset", "forgot", "register", "invitacion", "turno",
        "reservar", "pago",
    };

    /// <summary>
    /// Valida que un slug sea sintácticamente correcto y no use palabras prohibidas.
    /// No verifica disponibilidad en la base de datos.
    /// </summary>
    public static bool EsSlugValido(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        if (slug.Length < 3 || slug.Length > 30) return false;
        if (!_validSlugPattern.IsMatch(slug)) return false;
        if (PalabrasProhibidas.Contains(slug)) return false;
        return true;
    }

    public static string GenerateSlug(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return GenerateFallback();

        // Normalizar a FormKD descompone caracteres acentuados (á → a + combining accent)
        var normalized = nombre.Normalize(NormalizationForm.FormKD);

        // Eliminar caracteres no ASCII (marks de combinación, etc.)
        var ascii = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                ascii.Append(c);
        }

        var slug = ascii.ToString().ToLowerInvariant();
        slug = _nonAlphanumeric.Replace(slug, "-");
        slug = _trailingHyphens.Replace(slug, string.Empty);

        return string.IsNullOrEmpty(slug) ? GenerateFallback() : slug;
    }

    public static async Task<string> GenerateUniqueSlugAsync(
        string nombre,
        Func<string, Task<bool>> slugExists)
    {
        var baseSlug = GenerateSlug(nombre);

        if (!await slugExists(baseSlug))
            return baseSlug;

        for (int i = 2; i <= 99; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!await slugExists(candidate))
                return candidate;
        }

        // Fallback con sufijo aleatorio si todos los números están ocupados
        return $"{baseSlug}-{GenerateFallback()}";
    }

    private static string GenerateFallback()
        => Guid.NewGuid().ToString("N")[..8];
}
