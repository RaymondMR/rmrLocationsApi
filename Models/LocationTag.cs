namespace rmrLocationsApi.Models;

public class LocationTag
{
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public Guid? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

