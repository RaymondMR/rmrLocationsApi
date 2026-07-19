namespace rmrLocationsApi.Models;

public class UserFavoriteLocation
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

