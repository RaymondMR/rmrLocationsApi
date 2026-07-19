namespace rmrLocationsApi.Models;

public class Review : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>1..5</summary>
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateOnly? VisitedOn { get; set; }
}

