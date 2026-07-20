namespace rmrLocationsApi.Models;

public class CollectionItem
{
    // Navegaciones anulables para permitir binding en bodies (ver LocationCategory).
    public Guid CollectionId { get; set; }
    public LocationCollection? Collection { get; set; }

    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

