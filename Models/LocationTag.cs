using rmrLocationsApi.Validation;

namespace rmrLocationsApi.Models;

public class LocationTag
{
    // Navegaciones anulables para permitir el POST del grafo (ver LocationCategory).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    // Ver LocationCategory.CategoryId: Guid.Empty no es un id válido.
    [NotEmptyGuid]
    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }

    public Guid? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

