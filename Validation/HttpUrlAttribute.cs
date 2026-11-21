using System.ComponentModel.DataAnnotations;

namespace rmrLocationsApi.Validation;

/// <summary>
/// El texto, si no está vacío, debe ser una URL absoluta con esquema http o https.
/// El sitio web de un lugar se pinta como <c>href</c> en el frontend: sin esta regla se
/// podían guardar valores como <c>javascript:...</c> (XSS almacenado al hacer clic).
/// <c>null</c> y <c>""</c> son válidos ("sin sitio web"); la obligatoriedad se declara con [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;
        if (value is not string text)
            return false;
        if (text.Length == 0)
            return true;

        // Sin espacios en los extremos ni caracteres de control: los navegadores descartan
        // tabuladores y saltos de línea al interpretar una URL, así que no se deben guardar.
        if (text != text.Trim() || text.Any(char.IsControl))
            return false;

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0;
    }

    public override string FormatErrorMessage(string name)
        => $"The {name} field must be an absolute http or https URL.";
}
