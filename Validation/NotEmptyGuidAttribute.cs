using System.ComponentModel.DataAnnotations;

namespace rmrLocationsApi.Validation;

/// <summary>
/// El identificador no puede ser <see cref="Guid.Empty"/>. Un <c>Guid</c> no anulable nunca es null: si el
/// cliente lo omite (por ejemplo manda solo un objeto anidado, que el contrato de entrada ignora) llega como
/// <c>00000000-0000-0000-0000-000000000000</c> y EF no puede guardar la fila hija (InvalidOperationException,
/// es decir un 500). Con esta regla MVC responde 400 señalando el campo.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;

    public override string FormatErrorMessage(string name)
        => $"The {name} field must be a valid, non-empty identifier.";
}
