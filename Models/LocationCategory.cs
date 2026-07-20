namespace rmrLocationsApi.Models;

public class LocationCategory
{
    // Navegaciones anulables: si fueran non-nullable, la validación implícita de MVC
    // las exigiría en el body y rompería el POST del grafo (la relación la define la FK).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

