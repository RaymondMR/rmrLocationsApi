namespace rmrLocationsApi.Models;

public class LocationCategory
{
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

