namespace rmrLocationsApi.Models;

public class OpeningHour
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public bool IsClosed { get; set; }
}

