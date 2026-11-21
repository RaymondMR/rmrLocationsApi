using rmrLocationsApi.Validation;

namespace rmrLocationsApi.Models;

public class LocationCategory
{
    // Navegaciones anulables: si fueran non-nullable, la validación implícita de MVC
    // las exigiría en el body y rompería el POST del grafo (la relación la define la FK).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    // Guid.Empty no es un id válido: llega así cuando el cliente manda solo un 'category' anidado
    // (el contrato de entrada lo ignora) y sin esta regla EF fallaba con un 500.
    [NotEmptyGuid]
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

