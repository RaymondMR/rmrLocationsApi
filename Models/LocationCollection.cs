namespace rmrLocationsApi.Models;

public class LocationCollection : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    public string? Description { get; set; }
    public CollectionVisibility Visibility { get; set; } = CollectionVisibility.Private;

    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    public ICollection<CollectionItem> Items { get; set; } = new List<CollectionItem>();
}

