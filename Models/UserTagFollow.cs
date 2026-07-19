namespace rmrLocationsApi.Models;

public class UserTagFollow
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public bool NotifyOnNewLocation { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

