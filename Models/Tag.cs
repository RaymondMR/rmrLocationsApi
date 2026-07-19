namespace rmrLocationsApi.Models;

public class Tag : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    public string Slug { get; set; }
    public string? Description { get; set; }
    public int UsageCount { get; set; }

    public ICollection<LocationTag> LocationTags { get; set; } = new List<LocationTag>();
    public ICollection<UserTagFollow> Followers { get; set; } = new List<UserTagFollow>();
}

