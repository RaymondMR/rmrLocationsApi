namespace rmrLocationsApi.Models;

public class CollectionItem
{
    public Guid CollectionId { get; set; }
    public LocationCollection Collection { get; set; } = null!;

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

