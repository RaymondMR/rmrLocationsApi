namespace rmrLocationsApi.Models;

public class LocationTag
{
    // Navegaciones anulables para permitir el POST del grafo (ver LocationCategory).
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }

    public Guid? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

