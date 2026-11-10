using System.ComponentModel.DataAnnotations;

namespace TurneroApp.Application.Validation;

/// <summary>
/// Valida que la contraseña tenga una longitud mínima e incluya minúscula,
/// mayúscula, número y un carácter especial.
/// <para>
/// Solo se evalúa al <b>definir o cambiar</b> la contraseña. Las cuentas existentes
/// con contraseñas débiles no se ven afectadas: el login únicamente verifica el hash
/// BCrypt almacenado y nunca re-valida la complejidad.
/// </para>
/// Si el valor es null o vacío, la validación pasa (dejá que <c>[Required]</c> maneje
/// la obligatoriedad). Así los campos opcionales de contraseña no fallan cuando no se envían.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class StrongPasswordAttribute : ValidationAttribute
{
    public int MinLength { get; init; } = 8;

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not string pwd || string.IsNullOrEmpty(pwd))
            return ValidationResult.Success;

        var faltantes = new List<string>();
        if (pwd.Length < MinLength)                        faltantes.Add($"al menos {MinLength} caracteres");
        if (!pwd.Any(char.IsLower))                        faltantes.Add("una minúscula");
        if (!pwd.Any(char.IsUpper))                        faltantes.Add("una mayúscula");
        if (!pwd.Any(char.IsDigit))                        faltantes.Add("un número");
        if (!pwd.Any(c => !char.IsLetterOrDigit(c)))       faltantes.Add("un carácter especial");

        if (faltantes.Count == 0)
            return ValidationResult.Success;

        var msg = "La contraseña debe incluir " + string.Join(", ", faltantes) + ".";
        return new ValidationResult(
            msg,
            ctx.MemberName is null ? null : new[] { ctx.MemberName });
    }
}
